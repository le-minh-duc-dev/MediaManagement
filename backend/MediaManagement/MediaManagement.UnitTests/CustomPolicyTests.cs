using System.Globalization;
using MediaManagement.Entities;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace MediaManagement.UnitTests;

public class CustomPolicyTests
{
    private const string PrivateContent = "PRIVATE-CONTENT-MUST-NOT-APPEAR";
    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Theory]
    [InlineData("MediaAsset")]
    [InlineData("UploadSession")]
    [InlineData("Post")]
    [InlineData("PostItem")]
    public void Entity_logs_contain_only_approved_fields_even_when_nested(string entityKind)
    {
        var (entity, expectedFields) = CreateEntity(entityKind);
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .Destructure.With<CustomPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("Entity {Operation} {@Entity}", "Upload", entity);
        logger.Information("Nested {@Envelope}", new { Entity = entity });

        Assert.Equal(2, sink.Events.Count);
        foreach (var logEvent in sink.Events)
        {
            var structure = logEvent.Properties.TryGetValue("Entity", out var direct)
                ? Assert.IsType<StructureValue>(direct)
                : Assert.IsType<StructureValue>(
                    Assert
                        .Single(
                            Assert
                                .IsType<StructureValue>(logEvent.Properties["Envelope"])
                                .Properties
                        )
                        .Value
                );

            Assert.Equal(expectedFields.Order(), structure.Properties.Select(p => p.Name).Order());
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            new JsonFormatter().Format(logEvent, output);
            Assert.DoesNotContain(PrivateContent, output.ToString());
            Assert.DoesNotContain(OwnerId.ToString(), output.ToString());
            Assert.DoesNotContain("OwnerId", output.ToString());
        }

        Assert.Equal(
            "Upload",
            Assert.IsType<ScalarValue>(sink.Events[0].Properties["Operation"]).Value
        );
        var id = Assert
            .IsType<StructureValue>(sink.Events[0].Properties["Entity"])
            .Properties.Single(p => p.Name == "Id");
        Assert.Equal(
            entity.GetType().GetProperty("Id")!.GetValue(entity),
            Assert.IsType<ScalarValue>(id.Value).Value
        );
    }

    private static (object Entity, string[] ExpectedFields) CreateEntity(string kind)
    {
        var session = new UploadSession
        {
            Id = Guid.NewGuid(),
            CreatedBy = OwnerId,
            Status = UploadSessionStatus.Completed,
            ExpectedSizeBytes = 1024,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        };
        var asset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            CreatedBy = OwnerId,
            UploadSessionId = session.Id,
            FileName = PrivateContent,
            ObjectKey = PrivateContent,
            ContentType = MediaContentType.Png,
            SizeBytes = 1024,
            UploadSession = session,
        };
        session.MediaAssets.Add(asset);
        var post = new Post
        {
            Id = Guid.NewGuid(),
            CreatedBy = OwnerId,
            Caption = PrivateContent,
        };
        var item = new PostItem
        {
            Id = Guid.NewGuid(),
            PostId = post.Id,
            MediaAssetId = asset.Id,
            AltText = PrivateContent,
            Post = post,
            MediaAsset = asset,
        };
        post.Items.Add(item);
        post.Tags.Add(new Tag { Id = Guid.NewGuid(), Name = PrivateContent });

        return kind switch
        {
            "MediaAsset" => (
                asset,
                ["Id", "UploadSessionId", "ContentType", "SizeBytes", "CreatedAt"]
            ),
            "UploadSession" => (
                session,
                ["Id", "Status", "ExpectedSizeBytes", "CreatedAt", "ExpiresAt", "CompletedAt"]
            ),
            "Post" => (post, ["Id", "CreatedAt", "UpdatedAt"]),
            "PostItem" => (item, ["Id", "PostId", "MediaAssetId", "SortOrder"]),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    [Fact]
    public void Unrelated_objects_use_default_destructuring()
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .Destructure.With<CustomPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("Result {@Result}", new { Status = "Ready", Count = 3 });

        var properties = Assert
            .IsType<StructureValue>(Assert.Single(sink.Events).Properties["Result"])
            .Properties;
        Assert.Equal(
            "Ready",
            Assert.IsType<ScalarValue>(properties.Single(p => p.Name == "Status").Value).Value
        );
        Assert.Equal(
            3,
            Assert.IsType<ScalarValue>(properties.Single(p => p.Name == "Count").Value).Value
        );
    }

    [Fact]
    public void Projected_values_respect_configured_string_limit()
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .Destructure.With<CustomPolicy>()
            .Destructure.ToMaximumStringLength(5)
            .WriteTo.Sink(sink)
            .CreateLogger();
        logger.Information(
            "Asset {@Asset}",
            new MediaAsset
            {
                FileName = PrivateContent,
                ObjectKey = PrivateContent,
                ContentType = MediaContentType.Png,
            }
        );

        var properties = Assert
            .IsType<StructureValue>(Assert.Single(sink.Events).Properties["Asset"])
            .Properties;
        var contentType = Assert.IsType<string>(
            Assert.IsType<ScalarValue>(properties.Single(p => p.Name == "ContentType").Value).Value
        );
        Assert.True(contentType.Length <= 5);
        Assert.NotEqual("image/png", contentType);
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
