using System.Linq.Expressions;
using MediaManagement.Interfaces;

namespace MediaManagement.Models;

// GENERIC SPECIFICATION IMPLEMENTATION (BASE CLASS)
// https://github.com/dotnet-architecture/eShopOnWeb

public class SpecifcationBase<T>(
    Expression<Func<T, bool>>? criteria = default,
    int? take = default,
    int? skip = default
) : ISpecification<T>
{
    private const int DefaultTake = 20;
    private const int DefaultSkip = 0;

    public Expression<Func<T, bool>>? Criteria { get; } = criteria;
    public List<Expression<Func<T, object>>> Includes { get; } = [];
    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    public int? Take { get; } = take ?? DefaultTake;

    public int? Skip { get; } = skip ?? DefaultSkip;

    protected void AddInclude(Expression<Func<T, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }

    public SpecifcationBase<T> Include(Expression<Func<T, object>> includeExpression)
    {
        Includes.Add(includeExpression);
        return this;
    }

    protected void AddOrderBy(Expression<Func<T, object>> orderByExpression)
    {
        OrderBy = orderByExpression;
    }

    protected void AddOrderByDescending(Expression<Func<T, object>> orderByDescExpression)
    {
        OrderByDescending = orderByDescExpression;
    }
}
