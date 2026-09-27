using MediaManagement.Database;
using MediaManagement.Interfaces;
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
        ISpecification<T>? specification = default,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<T> query = _context.Set<T>().AsQueryable();

        if (specification != null)
        {
            query = query.AsNoTracking();
        }

        if (specification != null)
        {
            query = GetQuery(query, specification);
        }
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<ICollection<T>> GetAllAsync(
        ISpecification<T>? specification = default,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<T> query = _context.Set<T>().AsQueryable();

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (specification != null)
        {
            query = GetQuery(query, specification);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<T?> GetByIdAsync(
        Guid id,
        ISpecification<T>? specification = default,
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

    public static IQueryable<T> GetQuery(IQueryable<T> inputQuery, ISpecification<T> spec)
    {
        IQueryable<T> query = inputQuery;
        if (spec.Criteria != null)
        {
            query = query.Where(spec.Criteria);
        }
        if (spec.OrderBy != null)
        {
            query = query.OrderBy(spec.OrderBy);
        }
        if (spec.OrderByDescending != null)
        {
            query = query.OrderByDescending(spec.OrderByDescending);
        }

        if (spec.Skip.HasValue)
        {
            query = query.Skip(spec.Skip.Value);
        }

        if (spec.Take.HasValue)
        {
            query = query.Take(spec.Take.Value);
        }

        query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));

        return query;
    }
}
