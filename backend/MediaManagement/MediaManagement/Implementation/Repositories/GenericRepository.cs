using System.Linq.Expressions;
using MediaManagement.Database;
using MediaManagement.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MediaManagement.Implementation.Repositories;

public abstract class GenericRepository<T>(MediaManagementContext context) : IGenericRepository<T>
    where T : class
{
    protected readonly MediaManagementContext _context = context;

    public void Add(T entity)
    {
        _context.Set<T>().Add(entity);
    }

    public void AddRange(ICollection<T> entities)
    {
        _context.Set<T>().AddRange(entities);
    }

    public async Task<ICollection<T>> FindAsync(
        Expression<Func<T, bool>> expression,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<T> query = _context.Set<T>().Where(expression);
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<ICollection<T>> GetAllAsync(
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<T> query = _context.Set<T>().AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<T?> GetByIdAsync(
        Guid id,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<T> query = _context.Set<T>().Where(e => EF.Property<Guid>(e, "Id") == id);
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public void Remove(T entity)
    {
        _context.Set<T>().Remove(entity);
    }

    public void RemoveRange(ICollection<T> entities)
    {
        _context.Set<T>().RemoveRange(entities);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
