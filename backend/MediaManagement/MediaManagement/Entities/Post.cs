namespace MediaManagement.Entities;

public class Post : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid OwnerId => CreatedBy;
    public string? Caption { get; set; }
    public ICollection<PostItem> Items { get; set; } = [];
    public ICollection<Tag> Tags { get; set; } = [];
}
