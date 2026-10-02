using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using MediaManagement.BackgroundWorkers;
using MediaManagement.Contracts.Uploads;
using MediaManagement.Database;
using MediaManagement.Entities;
using MediaManagement.Implementation.Services;
using MediaManagement.Interfaces.Services;
using MediaManagement.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaManagement.IntegrationTests;

public sealed class UploadSessionTests : IAsyncLifetime
{
    private readonly UploadFactory factory = new();
    private HttpClient client = null!;
    private const string Route = "/api/v1/upload-sessions";

    public async Task InitializeAsync()
    {
        client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Test-Owner", Guid.NewGuid().ToString());
        await using var scope = factory.Services.CreateAsyncScope();
        await scope
            .ServiceProvider.GetRequiredService<MediaManagementContext>()
            .Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task Batch_creation_persists_assets_and_returns_upload_instructions_and_location()
    {
        var created = await CreateAsync();
        Assert.Equal(2, created.Items.Count);
        Assert.All(
            created.Items,
            x =>
            {
                Assert.Equal("PUT", x.Method);
                Assert.Equal("*", x.Headers["If-None-Match"]);
                Assert.Equal(x.ContentType.ToMimeType(), x.Headers["Content-Type"]);
                Assert.Equal(factory.Clock.GetUtcNow().AddMinutes(15), x.UrlExpiresAt);
            }
        );
        await using var scope = factory.Services.CreateAsyncScope();
        var session = await scope
            .ServiceProvider.GetRequiredService<MediaManagementContext>()
            .UploadSessions.Include(x => x.MediaAssets)
            .SingleAsync();
        Assert.Equal(30, session.ExpectedSizeBytes);
        Assert.Equal(2, session.MediaAssets.Count);
        Assert.All(session.MediaAssets, x => Assert.DoesNotContain(x.FileName, x.ObjectKey));
        var details = await client.GetFromJsonAsync<UploadSessionDetails>($"{Route}/{created.Id}");
        Assert.Equal("Pending", details!.Status);
    }

    [Fact]
    public async Task Partial_completion_is_verified_idempotent_and_cleanup_preserves_successes()
    {
        var created = await CreateAsync();
        var successful = created.Items[0];
        await PutAsync(successful);
        // An omitted object may exist in S3, but must still be removed.
        await PutAsync(created.Items[1]);
        var request = new CompleteUploadSessionRequest([successful.MediaAssetId]);
        var response = await client.PostAsJsonAsync($"{Route}/{created.Id}/complete", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<UploadSessionDetails>())!;
        Assert.Equal("Completed", result.Status);
        Assert.Single(result.Items, x => x.Uploaded);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"{Route}/{created.Id}/complete", request)).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Conflict,
            (
                await client.PostAsJsonAsync(
                    $"{Route}/{created.Id}/complete",
                    new CompleteUploadSessionRequest([])
                )
            ).StatusCode
        );
        factory.Clock.Advance(TimeSpan.FromHours(23));
        await CleanupAsync();
        Assert.Single(factory.Storage.Objects);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaManagementContext>();
        Assert.Equal(successful.MediaAssetId, (await db.MediaAssets.SingleAsync()).Id);
        Assert.NotNull((await db.UploadSessions.SingleAsync()).CleanedUpAt);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"{Route}/{created.Id}/complete", request)).StatusCode
        );
    }

    [Theory]
    [InlineData("missing", "upload.object_missing")]
    [InlineData("size", "upload.object_metadata_mismatch")]
    [InlineData("type", "upload.object_metadata_mismatch")]
    public async Task Completion_rejects_missing_or_mismatched_objects_without_partial_updates(
        string mode,
        string code
    )
    {
        var created = await CreateAsync();
        await PutAsync(created.Items[0]);
        if (mode != "missing")
        {
            await PutAsync(
                created.Items[1],
                mode == "size" ? 999 : null,
                mode == "type" ? "text/plain" : null
            );
        }

        var response = await client.PostAsJsonAsync(
            $"{Route}/{created.Id}/complete",
            new CompleteUploadSessionRequest(created.Items.Select(x => x.MediaAssetId).ToList())
        );
        await AssertErrorAsync(response, HttpStatusCode.Conflict, code);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaManagementContext>();
        Assert.All(await db.MediaAssets.ToListAsync(), x => Assert.Null(x.UploadedAt));
        Assert.Equal(UploadSessionStatus.Pending, (await db.UploadSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Authentication_ownership_and_membership_are_enforced()
    {
        var created = await CreateAsync();
        await AssertErrorAsync(
            await client.PostAsJsonAsync(
                $"{Route}/{created.Id}/complete",
                new CompleteUploadSessionRequest([Guid.NewGuid()])
            ),
            HttpStatusCode.BadRequest,
            "upload.asset_not_in_session"
        );
        client.DefaultRequestHeaders.Remove("X-Test-Owner");
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync($"{Route}/{created.Id}")).StatusCode
        );
        client.DefaultRequestHeaders.Add("X-Test-Owner", Guid.NewGuid().ToString());
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"{Route}/{created.Id}")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await client.PostAsJsonAsync(
                    $"{Route}/{created.Id}/complete",
                    new CompleteUploadSessionRequest([])
                )
            ).StatusCode
        );
    }

    [Theory]
    [InlineData("{\"items\":null}")]
    [InlineData("{\"items\":[]}")]
    [InlineData("{\"items\":[null]}")]
    [InlineData("{\"items\":[{\"fileName\":\"\",\"sizeBytes\":0,\"contentType\":\"text/html\"}]}")]
    public async Task Invalid_items_are_rejected_before_persistence(string json)
    {
        using var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(Route, body)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Empty(
            await scope
                .ServiceProvider.GetRequiredService<MediaManagementContext>()
                .UploadSessions.ToListAsync()
        );
    }

    [Fact]
    public async Task Empty_completion_marks_all_failed_and_duplicate_ids_are_rejected()
    {
        var created = await CreateAsync();
        var id = created.Items[0].MediaAssetId;
        await AssertErrorAsync(
            await client.PostAsJsonAsync(
                $"{Route}/{created.Id}/complete",
                new CompleteUploadSessionRequest([id, id])
            ),
            HttpStatusCode.BadRequest,
            "upload.asset_ids.duplicate"
        );
        var response = await client.PostAsJsonAsync(
            $"{Route}/{created.Id}/complete",
            new CompleteUploadSessionRequest([])
        );
        Assert.Equal(
            "Failed",
            (await response.Content.ReadFromJsonAsync<UploadSessionDetails>())!.Status
        );
        factory.Clock.Advance(TimeSpan.FromHours(23));
        await CleanupAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Empty(
            await scope
                .ServiceProvider.GetRequiredService<MediaManagementContext>()
                .MediaAssets.ToListAsync()
        );
    }

    [Fact]
    public async Task Abandoned_sessions_expire_and_failed_deletions_retry_without_losing_rows()
    {
        var created = await CreateAsync();
        await PutAsync(created.Items[0]);
        factory.Clock.Advance(TimeSpan.FromHours(1));
        await AssertErrorAsync(
            await client.PostAsJsonAsync(
                $"{Route}/{created.Id}/complete",
                new CompleteUploadSessionRequest([created.Items[0].MediaAssetId])
            ),
            HttpStatusCode.Conflict,
            "upload.session_expired"
        );
        await CleanupAsync();
        Assert.Empty(factory.Storage.Deleted);
        factory.Clock.Advance(TimeSpan.FromHours(22));
        factory.Storage.FailDeletes = true;
        await CleanupAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaManagementContext>();
            Assert.Equal(2, await db.MediaAssets.CountAsync());
            var session = await db.UploadSessions.SingleAsync();
            Assert.Equal(UploadSessionStatus.Expired, session.Status);
            Assert.Null(session.CleanedUpAt);
        }
        factory.Storage.FailDeletes = false;
        factory.Clock.Advance(TimeSpan.FromMinutes(5));
        await CleanupAsync();
        Assert.Empty(factory.Storage.Objects);
        Assert.Equal(2, factory.Storage.Deleted.Count);
        await using var finalScope = factory.Services.CreateAsyncScope();
        Assert.Empty(
            await finalScope
                .ServiceProvider.GetRequiredService<MediaManagementContext>()
                .MediaAssets.ToListAsync()
        );
    }

    [Fact]
    public async Task Concurrent_different_completion_cannot_replace_the_winning_asset_set()
    {
        var created = await CreateAsync();
        await PutAsync(created.Items[0]);
        await PutAsync(created.Items[1]);
        factory.Storage.OnMetadata = async () =>
        {
            factory.Storage.OnMetadata = null;
            var winner = await client.PostAsJsonAsync(
                $"{Route}/{created.Id}/complete",
                new CompleteUploadSessionRequest([created.Items[1].MediaAssetId])
            );
            Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        };
        var loser = await client.PostAsJsonAsync(
            $"{Route}/{created.Id}/complete",
            new CompleteUploadSessionRequest([created.Items[0].MediaAssetId])
        );
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        var details = (
            await client.GetFromJsonAsync<UploadSessionDetails>($"{Route}/{created.Id}")
        )!;
        Assert.Equal(
            created.Items[1].MediaAssetId,
            Assert.Single(details.Items, x => x.Uploaded).MediaAssetId
        );
    }

    [Fact]
    public async Task Real_S3_signer_signs_content_type_and_write_once_header_without_network()
    {
        using var s3 = new AmazonS3Client(
            new BasicAWSCredentials("test-key", "test-secret"),
            RegionEndpoint.APSoutheast1
        );
        var storage = new S3UploadStorage(
            s3,
            TimeProvider.System,
            Options.Create(new UploadOptions { BucketName = "test-media-bucket" })
        );
        var signed = await storage.CreateUploadUrlAsync(
            "uploads/test",
            "image/png",
            DateTimeOffset.UtcNow.AddMinutes(15),
            default
        );
        var url = Uri.UnescapeDataString(signed.Url);
        Assert.StartsWith("https://", url);
        Assert.Contains("X-Amz-SignedHeaders=", url);
        Assert.Contains("if-none-match", url);
        Assert.Contains("content-type", url);
        Assert.Contains("X-Amz-Expires=", url);
    }

    private async Task<CreatedUploadSession> CreateAsync()
    {
        var response = await client.PostAsJsonAsync(
            Route,
            new CreateUploadSessionRequest([
                new("photo.jpg", 10, MediaContentType.Jpeg),
                new("photo.jpg", 20, MediaContentType.Jpeg),
            ])
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CreatedUploadSession>())!;
        Assert.EndsWith($"{Route}/{created.Id}", response.Headers.Location!.ToString());
        return created;
    }

    private async Task PutAsync(UploadTarget target, long? size = null, string? type = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var key = await scope
            .ServiceProvider.GetRequiredService<MediaManagementContext>()
            .MediaAssets.Where(x => x.Id == target.MediaAssetId)
            .Select(x => x.ObjectKey)
            .SingleAsync();
        factory.Storage.Objects[key] = new(size ?? target.SizeBytes, type ?? target.ContentType.ToMimeType());
    }

    private async Task CleanupAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope
            .ServiceProvider.GetRequiredService<UploadCleanupService>()
            .RunBatchAsync(default);
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code
    )
    {
        Assert.True(
            response.StatusCode == status,
            $"Expected {(int)status} but received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"
        );
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            json.GetProperty("errors").EnumerateArray(),
            x => x.GetProperty("code").GetString() == code
        );
    }
}

internal sealed class UploadFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public UploadTestClock Clock { get; } = new();
    public FakeUploadStorage Storage { get; } = new();

    public UploadFactory() => connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<DbContextOptions<MediaManagementContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<MediaManagementContext>>();
            services.AddDbContext<MediaManagementContext>(options => options.UseSqlite(connection));
            services.RemoveAll<IUploadStorage>();
            services.AddSingleton<IUploadStorage>(Storage);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            var worker = services.Single(x =>
                x.ServiceType == typeof(IHostedService)
                && x.ImplementationType == typeof(UploadCleanupWorker)
            );
            services.Remove(worker);
            services
                .AddAuthentication("UploadTest")
                .AddScheme<AuthenticationSchemeOptions, UploadTestAuth>("UploadTest", _ => { });
        });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await connection.DisposeAsync();
    }
}

internal sealed class UploadTestClock : TimeProvider
{
    private DateTimeOffset now = new(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan duration) => now += duration;
}

internal sealed class FakeUploadStorage : IUploadStorage
{
    public ConcurrentDictionary<string, StoredUpload> Objects { get; } = new();
    public List<string> Deleted { get; } = [];
    public bool FailDeletes { get; set; }
    public Func<Task>? OnMetadata { get; set; }

    public Task<SignedUpload> CreateUploadUrlAsync(
        string key,
        string contentType,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult(
            new SignedUpload(
                $"https://storage.test/{key}",
                new Dictionary<string, string>
                {
                    ["Content-Type"] = contentType,
                    ["If-None-Match"] = "*",
                }
            )
        );

    public async Task<StoredUpload?> GetMetadataAsync(
        string key,
        CancellationToken cancellationToken
    )
    {
        if (OnMetadata is not null)
        {
            await OnMetadata();
        }

        return Objects.GetValueOrDefault(key);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        if (FailDeletes)
        {
            throw new IOException("Simulated S3 outage");
        }

        Objects.TryRemove(key, out _);
        Deleted.Add(key);
        return Task.CompletedTask;
    }

    [return: NotNull]
    public Task<Dictionary<string, string>> GetPresignedDownloadUrlsAsync(
        ICollection<string> keys,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            keys.Distinct(StringComparer.Ordinal).ToDictionary(
                key => key,
                key => $"https://storage.test/download/{key}",
                StringComparer.Ordinal
            )
        );
    }
}

internal sealed class UploadTestAuth(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Owner", out var owner))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim("sub", owner.ToString())], Scheme.Name)
        );
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name))
        );
    }
}
