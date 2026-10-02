using System.Text.Json.Serialization;

namespace MediaManagement.Contracts.Uploads;

[JsonConverter(typeof(MediaContentTypeJsonConverter))]
public enum MediaContentType
{
    [JsonStringEnumMemberName("image/jpeg")]
    Jpeg = 1,
    [JsonStringEnumMemberName("image/png")]
    Png,
    [JsonStringEnumMemberName("image/webp")]
    Webp,
    [JsonStringEnumMemberName("image/gif")]
    Gif,
    [JsonStringEnumMemberName("video/mp4")]
    Mp4,
}

public sealed class MediaContentTypeJsonConverter()
    : JsonStringEnumConverter<MediaContentType>(allowIntegerValues: false);

public static class MediaContentTypeExtensions
{
    public static string ToMimeType(this MediaContentType contentType) => contentType switch
    {
        MediaContentType.Jpeg => "image/jpeg",
        MediaContentType.Png => "image/png",
        MediaContentType.Webp => "image/webp",
        MediaContentType.Gif => "image/gif",
        MediaContentType.Mp4 => "video/mp4",
        _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unsupported media content type."),
    };

    public static MediaContentType ToMediaContentType(this string contentType) => contentType switch
    {
        "image/jpeg" => MediaContentType.Jpeg,
        "image/png" => MediaContentType.Png,
        "image/webp" => MediaContentType.Webp,
        "image/gif" => MediaContentType.Gif,
        "video/mp4" => MediaContentType.Mp4,
        _ => throw new ArgumentException("Unsupported media content type.", nameof(contentType)),
    };
}
