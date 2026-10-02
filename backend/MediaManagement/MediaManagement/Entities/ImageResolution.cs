namespace MediaManagement.Entities;

public class ImageResolution : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ImageId { get; set; }
    public Guid MediaAssetId { get; set; }
    public Image? Image { get; set; }
    public MediaAsset? MediaAsset { get; set; }
}
