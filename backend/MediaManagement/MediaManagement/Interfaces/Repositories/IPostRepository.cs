using MediaManagement.Entities;

namespace MediaManagement.Interfaces.Repositories;

public interface IPostRepository : IGenericRepository<Post>
{
    void AddItem(PostItem item);
    Task<Post?> GetByOwnerAsync(Guid ownerId, Guid id, bool asNoTracking, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Post> Items, int TotalCount)> GetPageAsync(Guid ownerId, int skip, int take, CancellationToken cancellationToken);
    Task<int> CountAvailableAssetsAsync(Guid ownerId, IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
    Task<List<Tag>> GetTagsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}