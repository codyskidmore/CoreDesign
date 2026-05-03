namespace CoreDesign.Data.Interfaces;

public interface ICudRepository<TContext, T> where TContext : DbContext where T : BaseEntity
{
    Task<bool> InsertAsync(T entity, Guid userId, CancellationToken token);
    Task<bool> InsertRangeAsync(IEnumerable<T> entities, Guid userId, CancellationToken token);
    Task<bool> DeleteAsync(Ulid id, Guid userId, CancellationToken token);
    Task<bool> DeleteRangeAsync(IEnumerable<T> entities, Guid userId, CancellationToken token);
    Task<bool> UpdateAsync(T entity, Guid userId, CancellationToken token);
    Task<bool> UpdateRangeAsync(IEnumerable<T> entities, Guid userId, CancellationToken token);
}