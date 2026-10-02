using System.Text.Json;
using MediaManagement.Contracts.Uploads;
using MediaManagement.Entities;
using MediaManagement.Models;
using Microsoft.Extensions.Options;

namespace MediaManagement.UnitTests;

public sealed class MediaContentTypeTests
{
    [Theory]
    [InlineData(MediaContentType.Jpeg, "image/jpeg")]
    [InlineData(MediaContentType.Png, "image/png")]
    [InlineData(MediaContentType.Webp, "image/webp")]
    [InlineData(MediaContentType.Gif, "image/gif")]
    [InlineData(MediaContentType.Mp4, "video/mp4")]
    public void Mime_types_round_trip_through_json_and_storage(MediaContentType contentType, string mimeType)
    {
        var json = JsonSerializer.Serialize(contentType);
        Assert.Equal($"\"{mimeType}\"", json);
        Assert.Equal(contentType, JsonSerializer.Deserialize<MediaContentType>(json));
        Assert.Equal(mimeType, contentType.ToMimeType());
        Assert.Equal(contentType, mimeType.ToMediaContentType());
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"1\"")]
    [InlineData("\"text/plain\"")]
    [InlineData("null")]
    public void Invalid_json_content_types_are_rejected(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MediaContentType>(json));
    }

    [Theory]
    [InlineData((MediaContentType)0)]
    [InlineData((MediaContentType)999)]
    [InlineData(MediaContentType.Mp4)]
    public void Undefined_or_disabled_types_fail_validation(MediaContentType contentType)
    {
        var validator = new UploadItemValidator(Options.Create(new UploadOptions
        {
            AllowedContentTypes = ["image/jpeg"],
        }));

        var result = validator.Validate(new UploadItemRequest("file", 10, contentType));

        Assert.Contains(result.Errors, error => error.PropertyName == "ContentType");
    }

    [Fact]
    public void Missing_content_type_fails_validation()
    {
        var request = JsonSerializer.Deserialize<UploadItemRequest>(
            """{"fileName":"photo.jpg","sizeBytes":10}""", JsonSerializerOptions.Web)!;
        var validator = new UploadItemValidator(Options.Create(new UploadOptions()));

        Assert.Contains(validator.Validate(request).Errors, error => error.PropertyName == "ContentType");
    }
}
