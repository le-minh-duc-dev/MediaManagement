using MediaManagement.Interfaces;

namespace MediaManagement.Implementation;

public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    public string? CorrelationId
    {
        get;
        set
        {
            if (field is not null)
            {
                throw new InvalidOperationException("CorrelationId has already been set.");
            }
            field = value;
        }
    }
}
