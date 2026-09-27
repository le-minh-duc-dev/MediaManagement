using System.Linq.Expressions;

namespace MediaManagement.Interfaces.Repositories;

public interface IGenericRepository<T>
    where T : class
{
    Task<T?> GetByIdAsync(
        Guid id,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    );
    Task<ICollection<T>> GetAllAsync(
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    );
    Task<ICollection<T>> FindAsync(
        Expression<Func<T, bool>> expression,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    );
    void Add(T entity);
    void AddRange(ICollection<T> entities);
    void Remove(T entity);
    void RemoveRange(ICollection<T> entities);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
