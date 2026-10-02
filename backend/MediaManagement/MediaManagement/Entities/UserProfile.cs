namespace MediaManagement.Entities;

public class UserProfile : AuditableEntity
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public Guid? AvatarImageId { get; set; }
    public Image? Avatar { get; set; }
}
