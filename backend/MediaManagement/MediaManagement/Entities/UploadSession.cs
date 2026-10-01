namespace MediaManagement.Entities;

public class UploadSession : AuditableEntity
{
    public Guid Id { get; set; }
    public long ExpectedSizeBytes { get; set; }
    public UploadSessionStatus Status { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset NextCleanupAt { get; set; }
    public DateTimeOffset? CleanedUpAt { get; set; }
    public Guid Revision { get; set; }
    public ICollection<MediaAsset> MediaAssets { get; set; } = [];
}
