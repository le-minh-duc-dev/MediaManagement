namespace MediaManagement.Entities;

public class UploadSession
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public long ExpectedSizeBytes { get; set; }
    public UploadSessionStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<MediaAsset> MediaAssets { get; set; } = [];
}

