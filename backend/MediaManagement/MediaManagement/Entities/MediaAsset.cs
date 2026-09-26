namespace MediaManagement.Entities;

public class MediaAsset
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid UploadSessionId { get; set; }
    public required string ObjectKey { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public UploadSession? UploadSession { get; set; }
}

