using MediaManagement.Database;
using MediaManagement.Entities;
using MediaManagement.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MediaManagement.Implementation.Repositories;

public class UploadSessionRepository(MediaManagementContext context)
    : GenericRepository<UploadSession>(context),
        IUploadSessionRepository
{
    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return false;
        }
    }
}
