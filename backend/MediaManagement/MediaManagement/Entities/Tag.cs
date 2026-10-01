namespace MediaManagement.Entities;

public class Tag : AuditableEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    public ICollection<Post> Posts { get; set; } = [];
}
