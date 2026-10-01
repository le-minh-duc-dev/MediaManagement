namespace MediaManagement.Entities;

public class PostItem : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid MediaAssetId { get; set; }
    public int SortOrder { get; set; }
    public string? AltText { get; set; }

    public Post? Post { get; set; }
    public MediaAsset? MediaAsset { get; set; }
}
