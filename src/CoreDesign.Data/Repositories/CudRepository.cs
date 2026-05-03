namespace CoreDesign.Data.Repositories;

public class CudRepository<TContext, T> : ICudRepository<TContext, T>
    where TContext : DbContext
    where T : BaseEntity
{
    private readonly TContext _ctx;
    private readonly DbSet<T> _dbSet;

    public CudRepository(TContext ctx)
    {
        _ctx = ctx;
        _dbSet = _ctx.Set<T>();
    }

    public async Task<bool> InsertAsync(T entity, Guid userId, CancellationToken token)
    {
        entity.InitializeAuditFields(userId);
        _dbSet.Add(entity);
        return await _ctx.SaveChangesAsync(token) > 0;
    }

    public async Task<bool> InsertRangeAsync(IEnumerable<T> entities, Guid userId, CancellationToken token)
    {
        var list = entities.ToImmutableList();
        list.ForEach(e => { e.InitializeAuditFields(userId); });
        await _dbSet.AddRangeAsync(list, token);
        return await _ctx.SaveChangesAsync(token) == list.Count();
    }

    public async Task<bool> DeleteAsync(Ulid id, Guid userId, CancellationToken token)
    {
        var entity = await _dbSet.FirstOrDefaultAsync(e => e.Id == id, token);
        if (entity == null)
            return false;
        entity.IsDeleted = true;
        entity.UpdateAuditFields(userId);
        _dbSet.Update(entity);
        return await _ctx.SaveChangesAsync(token) == 1;
    }

    public async Task<bool> DeleteRangeAsync(IEnumerable<T> entities, Guid userId, CancellationToken token)
    {
        var list = entities.ToImmutableList();
        list.ForEach(e =>
        {
            e.IsDeleted = true;
            e.UpdateAuditFields(userId);
        });
        _dbSet.UpdateRange(list);
        return await _ctx.SaveChangesAsync(token) == list.Count();
    }

    public async Task<bool> UpdateAsync(T entity, Guid userId, CancellationToken token)
    {
        entity.UpdateAuditFields(userId);
        _dbSet.Attach(entity);
        _ctx.Entry(entity).State = EntityState.Modified;
        return await _ctx.SaveChangesAsync(token) == 1;
    }

    public async Task<bool> UpdateRangeAsync(IEnumerable<T> entities, Guid userId, CancellationToken token)
    {
        var list = entities.ToImmutableList();
        list.ForEach(e => { e.UpdateAuditFields(userId); });
        _dbSet.AttachRange(list);
        list.ForEach(e => _ctx.Entry(e).State = EntityState.Modified);
        return await _ctx.SaveChangesAsync(token) == list.Count();
    }
}