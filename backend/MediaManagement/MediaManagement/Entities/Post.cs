namespace MediaManagement.Entities;

public class Post
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string? Caption { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public ICollection<PostItem> Items { get; set; } = [];
    public ICollection<Tag> Tags { get; set; } = [];
}
