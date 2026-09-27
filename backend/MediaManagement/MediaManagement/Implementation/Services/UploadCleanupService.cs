using MediaManagement.Database;
using MediaManagement.Entities;
using MediaManagement.Interfaces.Services;
using MediaManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaManagement.Implementation.Services;

public sealed class UploadCleanupService(
    MediaManagementContext db,
    IUploadStorage storage,
    TimeProvider clock,
    IOptions<UploadOptions> options,
    ILogger<UploadCleanupService> logger
)
{
    public async Task<int> RunBatchAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        List<Guid> ids = await db
            .UploadSessions.AsNoTracking()
            .Where(x => x.CleanedUpAt == null && x.NextCleanupAt <= now)
            .OrderBy(x => x.NextCleanupAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(options.Value.CleanupBatchSize)
            .ToListAsync(cancellationToken);
        foreach (Guid id in ids)
        {
            try
            {
                // Change the concurrency token before deleting anything. Stale completions cannot commit.
                await db
                    .UploadSessions.Where(x =>
                        x.Id == id && x.Status == UploadSessionStatus.Pending && x.ExpiresAt <= now
                    )
                    .ExecuteUpdateAsync(
                        setters =>
                            setters
                                .SetProperty(x => x.Status, UploadSessionStatus.Expired)
                                .SetProperty(x => x.Revision, Guid.NewGuid()),
                        cancellationToken
                    );
                // Back off failures so one poisoned session cannot starve subsequent batches.
                await db
                    .UploadSessions.Where(x => x.Id == id)
                    .ExecuteUpdateAsync(
                        setters =>
                            setters.SetProperty(
                                x => x.NextCleanupAt,
                                now.AddMinutes(options.Value.CleanupIntervalMinutes)
                            ),
                        cancellationToken
                    );
                UploadSession session = await db
                    .UploadSessions.Include(x => x.MediaAssets)
                    .SingleAsync(x => x.Id == id, cancellationToken);
                if (session.Status == UploadSessionStatus.Pending || session.CleanedUpAt != null)
                {
                    continue;
                }

                foreach (
                    MediaAsset? asset in session
                        .MediaAssets.Where(x => x.UploadedAt == null)
                        .ToArray()
                )
                {
                    // S3 deletion is idempotent. Retain DB rows on storage failure so the next pass retries.
                    await storage.DeleteAsync(asset.ObjectKey, cancellationToken);
                    db.MediaAssets.Remove(asset);
                }
                session.CleanedUpAt = clock.GetUtcNow();
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Upload cleanup failed for session {UploadSessionId}; will retry",
                    id
                );
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }
        return ids.Count;
    }
}
