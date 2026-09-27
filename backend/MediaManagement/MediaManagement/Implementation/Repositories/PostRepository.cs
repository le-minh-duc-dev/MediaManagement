using MediaManagement.Database;
using MediaManagement.Entities;
using MediaManagement.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MediaManagement.Implementation.Repositories;

public sealed class PostRepository(MediaManagementContext context) : GenericRepository<Post>(context), IPostRepository
{
    public void AddItem(PostItem item) => _context.Set<PostItem>().Add(item);

    public Task<Post?> GetByOwnerAsync(Guid ownerId, Guid id, bool asNoTracking, CancellationToken cancellationToken)
    {
        IQueryable<Post> query = _context.Posts
            .Include(x => x.Items)
            .ThenInclude(x => x.MediaAsset)
            .Include(x => x.Tags);
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }
        return query.SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Post> Items, int TotalCount)> GetPageAsync(Guid ownerId, int skip, int take, CancellationToken cancellationToken)
    {
        IQueryable<Post> query = _context.Posts.AsNoTracking().Where(x => x.OwnerId == ownerId);
        int count = await query.CountAsync(cancellationToken);
        List<Post> items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(take)
            .Include(x => x.Items)
            .ThenInclude(x => x.MediaAsset)
            .Include(x => x.Tags)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return (items, count);
    }

    public Task<int> CountAvailableAssetsAsync(Guid ownerId, IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        _context.MediaAssets.CountAsync(x => ids.Contains(x.Id) && x.OwnerId == ownerId
            && x.UploadedAt != null && x.UploadSession != null
            && x.UploadSession.Status == UploadSessionStatus.Completed, cancellationToken);

    public Task<List<Tag>> GetTagsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        _context.Set<Tag>().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
}
