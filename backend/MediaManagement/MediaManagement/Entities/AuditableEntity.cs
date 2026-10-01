using MediaManagement.Interfaces.Entities;

namespace MediaManagement.Entities;

/// <summary>
/// Audit properties will be automatically set by the system when the entity is created or updated.
/// </summary>
public abstract class AuditableEntity : IAuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
