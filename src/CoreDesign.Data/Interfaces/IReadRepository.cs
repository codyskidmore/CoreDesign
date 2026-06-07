namespace CoreDesign.Data.Interfaces;

public interface IReadRepository<TContext, T> where TContext : DbContext where T : BaseEntity
{
    Task<IList<T>> GetAllAsync(Expression<
            Func<T, bool>>? whereExpression = null,
        Func<IQueryable<T>, IQueryable<T>>? orderBy = null,
        Func<IQueryable<T>, IQueryable<T>>? includes = null, CancellationToken token = default);

    Task<IList<T>> GetAllAttachedAsync(CancellationToken token = default);

    Task<IList<T>> GetAllAsync(CancellationToken token = default);

    Task<T?> GetAsync(Expression<Func<T, bool>> expression, Func<IQueryable<T>, IQueryable<T>>? includes = null,
        CancellationToken token = default);

    Task<T?> GetAttachedAsync(Expression<Func<T, bool>> expression, Func<IQueryable<T>, IQueryable<T>>? includes = null,
        CancellationToken token = default);
}