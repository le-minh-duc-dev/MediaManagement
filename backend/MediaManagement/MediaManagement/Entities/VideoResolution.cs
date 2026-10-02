namespace MediaManagement.Entities;

public class VideoResolution : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public Guid MediaAssetId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public Video? Video { get; set; }
    public MediaAsset? MediaAsset { get; set; }
}
