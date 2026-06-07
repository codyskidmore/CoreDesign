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

    public async Task<IList<T>> GetAllAsync(Expression<Func<T, bool>>? whereExpression = null, Func<IQueryable<T>,
            IQueryable<T>>? orderBy = null, Func<IQueryable<T>, IQueryable<T>>? includes = null,
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

    public async Task<T?> GetAsync(Expression<Func<T, bool>> expression,
        Func<IQueryable<T>, IQueryable<T>>? includes = null,
        CancellationToken token = default)
    {
        IQueryable<T> query = _dbSet;
        if (includes != null) query = includes(query);
        return await query.AsNoTracking().FirstOrDefaultAsync(expression, token);
    }

    public async Task<T?> GetAttachedAsync(Expression<Func<T, bool>> expression,
        Func<IQueryable<T>, IQueryable<T>>? includes = null, CancellationToken token = default)
    {
        IQueryable<T> query = _dbSet;
        if (includes != null) query = includes(query);
        return await query.FirstOrDefaultAsync(expression, token);
    }
}