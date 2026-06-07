namespace CoreDesign.Data.Interfaces;

public interface ICudRepository<TContext, T> where TContext : DbContext where T : BaseEntity
{
    Task<bool> InsertAsync(T entity, CancellationToken token);
    Task<bool> InsertRangeAsync(IEnumerable<T> entities, CancellationToken token);
    Task<bool> UpdateAsync(T entity, CancellationToken token);
    Task<bool> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken token);
    Task<bool> SoftDeleteAsync(Ulid id, CancellationToken token);
    Task<bool> SoftDeleteRangeAsync(IEnumerable<T> entities, CancellationToken token);
    Task<bool> SoftDeleteCascadeAsync(Ulid id, CancellationToken token);
    Task<bool> HardDeleteAsync(Ulid id, CancellationToken token);
    Task<bool> HardDeleteCascadeAsync(Ulid id, CancellationToken token);
}
