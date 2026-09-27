using MediaManagement.Interfaces.Services;

namespace MediaManagement.Implementation.Services;

public class ServiceBase : IServiceBase
{
    protected ICurrentUser CurrentUser { get; }

    protected ServiceBase(ICurrentUser currentUser)
    {
        CurrentUser = currentUser;
    }

    public Guid CreateNewGuid()
    {
        return Guid.CreateVersion7();
    }
}
