namespace MediaManagement.Entities;

public enum MediaContentType
{
    Jpeg = 1,
    Png = 2,
    Webp = 3,
    Gif = 4,
    Mp4 = 5,
}

public static class MediaContentTypeExtensions
{
    public static string ToMimeType(this MediaContentType contentType) =>
        contentType switch
        {
            MediaContentType.Jpeg => "image/jpeg",
            MediaContentType.Png => "image/png",
            MediaContentType.Webp => "image/webp",
            MediaContentType.Gif => "image/gif",
            MediaContentType.Mp4 => "video/mp4",
            _ => throw new ArgumentOutOfRangeException(
                nameof(contentType),
                contentType,
                "Unsupported media content type."
            ),
        };

    public static MediaContentType ToMediaContentType(this string contentType) =>
        contentType switch
        {
            "image/jpeg" => MediaContentType.Jpeg,
            "image/png" => MediaContentType.Png,
            "image/webp" => MediaContentType.Webp,
            "image/gif" => MediaContentType.Gif,
            "video/mp4" => MediaContentType.Mp4,
            _ => throw new ArgumentException(
                "Unsupported media content type.",
                nameof(contentType)
            ),
        };
}

