namespace MediaManagement.Interfaces.Entities;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }

    DateTimeOffset? UpdatedAt { get; set; }

    Guid CreatedBy { get; set; }

    Guid? UpdatedBy { get; set; }
}
