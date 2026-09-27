namespace MediaManagement.Interfaces;

public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; set; }
}
