using MediaManagement.Database;
using MediaManagement.Entities;
using MediaManagement.Interfaces.Repositories;

namespace MediaManagement.Implementation.Repositories;

public class UploadSessionRepository(MediaManagementContext context)
    : GenericRepository<UploadSession>(context),
        IUploadSessionRepository { }
