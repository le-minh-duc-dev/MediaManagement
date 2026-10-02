namespace MediaManagement.Entities;

public class Video : AuditableEntity
{
    public Guid Id { get; set; }
    public ICollection<VideoResolution> Resolutions { get; set; } = [];
    public Guid? ThumbnailImageId { get; set; }
    public Image? Thumbnail { get; set; }
}
