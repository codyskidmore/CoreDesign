namespace CoreDesign.Data.Repositories;

public class ReadRepository<TContext, T> : IReadRepository<TContext, T>
    where TContext : DbContext
    where T : BaseEntity
{
    private readonly TContext _ctx;
    private readonly DbSet<T> _dbSet;

    public ReadRepository(TContext ctx)
    {
        _ctx = ctx;
        _dbSet = _ctx.Set<T>();
    }

    /// <summary>
    ///     Retrieves a list of entities from the database based on the specified criteria.
    /// </summary>
    /// <param name="whereExpression">An optional filter expression to apply to the query.</param>
    /// <param name="orderBy">An optional function to order the results.</param>
    /// <param name="includes">An optional list of related entities to include in the query.</param>
    /// <returns>A list of entities that match the specified criteria.</returns>
    /// <example>myRepository.GetAll(e => e.CreatedBy == "userId", e => e.CreatedAt)</example>
    public async Task<IList<T>> GetAllAsync(Expression<Func<T, bool>> whereExpression = null, Func<IQueryable<T>,
            IQueryable<T>> orderBy = null, Func<IQueryable<T>, IQueryable<T>> includes = null,
        CancellationToken token = default)
    {
        IQueryable<T> query = _dbSet;

        if (whereExpression != null) query = query.Where(whereExpression);

        if (includes != null) query = includes(query);

        if (orderBy != null) query = orderBy(query);

        return await query.AsNoTracking().ToListAsync(token);
    }

    public async Task<IList<T>> GetAllAttachedAsync(CancellationToken token = default)
    {
        return await _dbSet.ToListAsync(token);
    }

    public async Task<IList<T>> GetAllAsync(CancellationToken token = default)
    {
        return await _dbSet.AsNoTracking().ToListAsync(token);
    }

    /// <summary>
    ///     The includes parameter is a list of child entities to also include in the query
    ///     result. Each string is the explicit namespace path to the child entity.
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="includes"></param>
    /// <returns></returns>
    public async Task<T> GetAsync(Expression<Func<T, bool>> expression,
        Func<IQueryable<T>, IQueryable<T>> includes = null,
        CancellationToken token = default)
    {
        IQueryable<T> query = _dbSet;
        if (includes != null) query = includes(query);
        return await query.AsNoTracking().FirstOrDefaultAsync(expression, token);
    }

    public async Task<T> GetAttachedAsync(Expression<Func<T, bool>> expression,
        Func<IQueryable<T>, IQueryable<T>> includes = null, CancellationToken token = default)
    {
        IQueryable<T> query = _dbSet;
        if (includes != null) query = includes(query);
        return await query.FirstOrDefaultAsync(expression, token);
    }
}