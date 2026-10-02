using MediaManagement.Entities;

namespace MediaManagement.Interfaces.Repositories;

public interface IUploadSessionRepository : IGenericRepository<UploadSession>
{
    // Returns false on a concurrency conflict and clears tracked changes before a fresh read.
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);
}
