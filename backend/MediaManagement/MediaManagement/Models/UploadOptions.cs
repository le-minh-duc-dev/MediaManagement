namespace MediaManagement.Models;

public sealed class UploadOptions
{
    public const string SectionName = "Uploads";
    public string BucketName { get; set; } = "";
    public string Region { get; set; } = "ap-southeast-1";
    public int MaxItems { get; set; } = 20;
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024;
    public string[] AllowedContentTypes { get; set; } =
    ["image/jpeg", "image/png", "image/webp", "image/gif", "video/mp4"];
    public int UrlLifetimeMinutes { get; set; } = 15;
    public int SessionLifetimeMinutes { get; set; } = 60;
    public int CleanupAfterHours { get; set; } = 23;
    public int CleanupIntervalMinutes { get; set; } = 5;
    public int CleanupBatchSize { get; set; } = 100;
    public bool CleanupEnabled { get; set; } = true;
    public int DownloadUrlLifetimeMinutes { get; set; } = 60;
}
