using MediaManagement.Database;
using MediaManagement.Entities;
using MediaManagement.Interfaces.Repositories;

namespace MediaManagement.Implementation.Repositories;

public class UploadSessionRepository : GenericRepository<UploadSession>, IUploadSessionRepository
{
    public UploadSessionRepository(MediaManagementContext context)
        : base(context) { }
}
