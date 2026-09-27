namespace MediaManagement.Interfaces.Repositories;

public interface IGenericRepository<T>
    where T : class
{
    Task<T?> GetByIdAsync(
        Guid id,
        ISpecification<T>? specification = default,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    );
    Task<ICollection<T>> GetAllAsync(
        ISpecification<T>? specification = default,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    );
    Task<ICollection<T>> FindAsync(
        ISpecification<T>? specification = default,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    );
    void Add(T entity);
    void AddRange(ICollection<T> entities);
    void Remove(T entity);
    void RemoveRange(ICollection<T> entities);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // Returns false on a concurrency conflict and discards tracked changes before a fresh read.
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);
}
