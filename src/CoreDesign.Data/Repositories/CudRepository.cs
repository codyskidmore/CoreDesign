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

    public async Task<bool> InsertAsync(T entity, CancellationToken token)
    {
        _dbSet.Add(entity);
        return await _ctx.SaveChangesAsync(token) > 0;
    }

    public async Task<bool> InsertRangeAsync(IEnumerable<T> entities, CancellationToken token)
    {
        var list = entities.ToImmutableList();
        await _dbSet.AddRangeAsync(list, token);
        return await _ctx.SaveChangesAsync(token) == list.Count;
    }

    public async Task<bool> UpdateAsync(T entity, CancellationToken token)
    {
        _dbSet.Attach(entity);
        _ctx.Entry(entity).State = EntityState.Modified;
        return await _ctx.SaveChangesAsync(token) == 1;
    }

    public async Task<bool> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken token)
    {
        var list = entities.ToImmutableList();
        _dbSet.AttachRange(list);
        list.ForEach(e => _ctx.Entry(e).State = EntityState.Modified);
        return await _ctx.SaveChangesAsync(token) == list.Count;
    }

    public async Task<bool> SoftDeleteAsync(Ulid id, CancellationToken token)
    {
        var entity = await _dbSet.FirstOrDefaultAsync(e => e.Id == id, token);
        if (entity is null) return false;
        entity.IsDeleted = true;
        return await _ctx.SaveChangesAsync(token) == 1;
    }

    public async Task<bool> SoftDeleteRangeAsync(IEnumerable<T> entities, CancellationToken token)
    {
        var list = entities.ToImmutableList();
        list.ForEach(e => e.IsDeleted = true);
        _dbSet.UpdateRange(list);
        return await _ctx.SaveChangesAsync(token) == list.Count;
    }

    public async Task<bool> SoftDeleteCascadeAsync(Ulid id, CancellationToken token)
    {
        var entity = await _dbSet.FirstOrDefaultAsync(e => e.Id == id, token);
        if (entity is null) return false;
        entity.IsDeleted = true;
        await SoftDeleteGraphAsync(entity, new HashSet<BaseEntity>(ReferenceEqualityComparer.Instance), token);
        return await _ctx.SaveChangesAsync(token) > 0;
    }

    public async Task<bool> HardDeleteAsync(Ulid id, CancellationToken token)
    {
        var entity = await _dbSet.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id, token);
        if (entity is null) return false;
        _dbSet.Remove(entity);
        return await _ctx.SaveChangesAsync(token) > 0;
    }

    public async Task<bool> HardDeleteCascadeAsync(Ulid id, CancellationToken token)
    {
        var entity = await _dbSet.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id, token);
        if (entity is null) return false;
        await HardDeleteGraphAsync(entity, new HashSet<BaseEntity>(ReferenceEqualityComparer.Instance), token);
        return await _ctx.SaveChangesAsync(token) > 0;
    }

    private async Task SoftDeleteGraphAsync(BaseEntity entity, HashSet<BaseEntity> visited, CancellationToken ct)
    {
        if (!visited.Add(entity)) return;

        var entry = _ctx.Entry(entity);
        var entityType = _ctx.Model.FindEntityType(entity.GetType())!;

        foreach (var nav in entityType.GetNavigations().Where(n =>
            n.IsCollection && typeof(BaseEntity).IsAssignableFrom(n.TargetEntityType.ClrType)))
        {
            await entry.Collection(nav.Name).LoadAsync(ct);

            foreach (var child in entry.Collection(nav.Name).CurrentValue!.Cast<BaseEntity>())
            {
                child.IsDeleted = true;
                await SoftDeleteGraphAsync(child, visited, ct);
            }
        }
    }

    private async Task HardDeleteGraphAsync(BaseEntity entity, HashSet<BaseEntity> visited, CancellationToken ct)
    {
        if (!visited.Add(entity)) return;

        var entry = _ctx.Entry(entity);
        var entityType = _ctx.Model.FindEntityType(entity.GetType())!;

        foreach (var nav in entityType.GetNavigations().Where(n =>
            n.IsCollection && typeof(BaseEntity).IsAssignableFrom(n.TargetEntityType.ClrType)))
        {
            await entry.Collection(nav.Name).LoadAsync(ct);

            foreach (var child in entry.Collection(nav.Name).CurrentValue!.Cast<BaseEntity>())
                await HardDeleteGraphAsync(child, visited, ct);
        }

        _ctx.Remove(entity);
    }
}
