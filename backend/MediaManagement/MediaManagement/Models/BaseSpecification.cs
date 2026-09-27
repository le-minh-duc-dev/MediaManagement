using System.Linq.Expressions;
using MediaManagement.Interfaces;

namespace MediaManagement.Models;

// GENERIC SPECIFICATION IMPLEMENTATION (BASE CLASS)
// https://github.com/dotnet-architecture/eShopOnWeb

public class BaseSpecifcation<T> : ISpecification<T>
{
    private const int DefaultTake = 20;
    private const int DefaultSkip = 0;

    public BaseSpecifcation()
        : this(null, null, null) { }

    public BaseSpecifcation(int? take, int? skip)
        : this(null, take, skip) { }

    public BaseSpecifcation(Expression<Func<T, bool>>? criteria, int? take, int? skip)
    {
        Criteria = criteria;
        Take = take ?? DefaultTake;
        Skip = skip ?? DefaultSkip;
    }

    public Expression<Func<T, bool>>? Criteria { get; }
    public List<Expression<Func<T, object>>> Includes { get; } = [];
    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    public int? Take { get; }

    public int? Skip { get; }

    protected void AddInclude(Expression<Func<T, object>> includeExpression)
    {
        Includes.Add(includeExpression);
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
