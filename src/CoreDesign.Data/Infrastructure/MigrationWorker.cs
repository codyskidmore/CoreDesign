using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoreDesign.Data.Infrastructure;

/// <summary>
/// Abstract base class for Aspire-hosted EF Core migration workers. Handles database
/// creation, migration, and an optional seeding step, then signals the host to stop.
/// Inherit and override <see cref="SeedAsync"/> to add application-specific seed data.
/// </summary>
public abstract class MigrationWorker<TContext>(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("Migrating database", ActivityKind.Client);
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

            await EnsureDatabaseAsync(dbContext, cancellationToken);
            await RunMigrationAsync(dbContext, cancellationToken);
            await SeedAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        lifetime.StopApplication();
    }

    /// <summary>
    /// Override to insert seed data after migrations complete. The default implementation
    /// is a no-op. Call <see cref="SeedEntitiesAsync{T}"/> for each entity set to seed.
    /// </summary>
    protected virtual Task SeedAsync(TContext dbContext, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Inserts each entity that does not already exist in the database, identified by
    /// <see cref="BaseEntity.Id"/>. Existing rows (including soft-deleted ones) are skipped.
    /// All writes are wrapped in the context's execution strategy so transient SQL Server
    /// errors are retried automatically.
    /// </summary>
    protected async Task SeedEntitiesAsync<T>(
        TContext dbContext,
        IEnumerable<T> entities,
        CancellationToken cancellationToken)
        where T : BaseEntity
    {
        using var activity = ActivitySource.StartActivity("Seeding database", ActivityKind.Client);
        try
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                foreach (var entity in entities)
                {
                    // IgnoreQueryFilters so soft-deleted rows are counted as existing.
                    var exists = await dbContext.Set<T>()
                        .AsNoTracking()
                        .IgnoreQueryFilters()
                        .AnyAsync(x => x.Id == entity.Id, cancellationToken);

                    if (!exists)
                        dbContext.Set<T>().Add(entity);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            });
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }
    }

    private async Task EnsureDatabaseAsync(TContext dbContext, CancellationToken cancellationToken)
    {
        var dbCreator = dbContext.GetService<IRelationalDatabaseCreator>();
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            if (!await dbCreator.ExistsAsync(cancellationToken))
                await dbCreator.CreateAsync(cancellationToken);
        });
    }

    private async Task RunMigrationAsync(TContext dbContext, CancellationToken cancellationToken)
    {
        logger.LogInformation("Migrating database.");
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(() => dbContext.Database.MigrateAsync(cancellationToken));
        logger.LogInformation("Database migration completed successfully.");
    }
}
