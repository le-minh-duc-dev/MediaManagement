namespace MediaManagement.Entities;

public class Image : AuditableEntity
{
    public Guid Id { get; set; }

    public ICollection<ImageResolution> Resolutions { get; set; } = [];

    public static Image CreateDefaultImageWithMediaAssetId(
        Guid newImageId,
        Guid newImageResolutionId,
        Guid mediaAssetId
    )
    {
        return new Image
        {
            Id = newImageId,
            Resolutions =
            [
                new ImageResolution
                {
                    Id = newImageResolutionId,
                    ImageId = newImageId,
                    MediaAssetId = mediaAssetId,
                },
            ],
        };
    }
}
