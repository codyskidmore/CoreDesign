using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoreDesign.Data.Infrastructure;

/// <summary>
/// Aspire-hosted EF Core migration worker. Handles database creation, migration, and an
/// optional seeding step, then signals the host to stop. By default, seeds from JSON files
/// in <paramref name="seedDirectory"/> where each filename is the fully qualified type name
/// of a <see cref="BaseEntity"/> subclass. Override <see cref="SeedAsync"/> for custom logic.
/// </summary>
public class MigrationWorker<TContext>(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker<TContext>> logger,
    string seedDirectory = "SeedData",
    ISet<string>? purgeBeforeSeed = null,
    int maxRetryCount = 10,
    TimeSpan retryDelay = default) : BackgroundService
    where TContext : DbContext
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly JsonSerializerOptions SeedJsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("Migrating database", ActivityKind.Client);
        var delay = retryDelay == default ? TimeSpan.FromSeconds(3) : retryDelay;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

                await EnsureDatabaseAsync(dbContext, cancellationToken);
                await RunMigrationAsync(dbContext, cancellationToken);
                await SeedAsync(dbContext, cancellationToken);
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < maxRetryCount)
            {
                activity?.AddException(ex);
                logger.LogWarning(ex,
                    "Migration attempt {Attempt} of {MaxRetries} failed. Retrying in {Delay}s.",
                    attempt, maxRetryCount, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                throw;
            }
        }

        lifetime.StopApplication();
    }

    /// <summary>
    /// Seeds the database from JSON files in the configured seed directory. Override to
    /// replace or extend this behavior. Call <see cref="SeedFromDirectoryAsync"/> or
    /// <see cref="SeedEntitiesAsync{T}"/> as needed.
    /// </summary>
    protected virtual Task SeedAsync(TContext dbContext, CancellationToken cancellationToken)
        => SeedFromDirectoryAsync(dbContext, seedDirectory, typeof(TContext).Assembly, cancellationToken);

    /// <summary>
    /// Scans <paramref name="directory"/> for <c>*.json</c> files and seeds each one.
    /// Each filename (without extension) must be the fully qualified type name of a
    /// <see cref="BaseEntity"/> subclass in <paramref name="typeAssembly"/>, for example
    /// <c>MyApp.Orders.Models.Order.json</c>. Files whose name cannot be resolved to a
    /// type are skipped with a warning.
    /// <para>
    /// Types listed in <c>purgeBeforeSeed</c> (by short or fully qualified name) have
    /// their table cleared before the seed file is applied. Configure this at runtime via
    /// <c>MigrationWorker:PurgeBeforeSeed</c> in appsettings or environment variables.
    /// </para>
    /// <para>
    /// <b>Warning:</b> purging is destructive and unrecoverable outside of a database
    /// backup. Use with extreme caution and only in non-Production environments (local
    /// development, staging seed iteration). Never configure <c>PurgeBeforeSeed</c> for a
    /// Production database.
    /// </para>
    /// </summary>
    protected async Task SeedFromDirectoryAsync(
        TContext dbContext,
        string directory,
        Assembly typeAssembly,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            logger.LogWarning("Seed directory '{Directory}' does not exist. Skipping.", directory);
            return;
        }

        var files = Directory.GetFiles(directory, "*.json");
        if (files.Length == 0)
        {
            logger.LogInformation("No seed files found in '{Directory}'.", directory);
            return;
        }

        var seedMethod = typeof(MigrationWorker<TContext>)
            .GetMethod(nameof(SeedEntitiesAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;
        var purgeMethod = typeof(MigrationWorker<TContext>)
            .GetMethod(nameof(PurgeEntitiesAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var filePath in files)
        {
            var typeName = Path.GetFileNameWithoutExtension(filePath);
            var entityType = typeAssembly.GetType(typeName);

            if (entityType is null)
            {
                logger.LogWarning(
                    "Could not resolve type '{TypeName}' in assembly '{Assembly}'. Skipping '{FileName}'.",
                    typeName, typeAssembly.GetName().Name, Path.GetFileName(filePath));
                continue;
            }

            if (!typeof(BaseEntity).IsAssignableFrom(entityType))
            {
                logger.LogWarning(
                    "Type '{TypeName}' does not inherit BaseEntity. Skipping '{FileName}'.",
                    typeName, Path.GetFileName(filePath));
                continue;
            }

            if (ShouldPurge(typeName, entityType.Name))
            {
                var genericPurgeMethod = purgeMethod.MakeGenericMethod(entityType);
                await (Task)genericPurgeMethod.Invoke(this, [dbContext, cancellationToken])!;
            }

            var json = File.ReadAllText(filePath);
            var listType = typeof(List<>).MakeGenericType(entityType);
            var entities = JsonSerializer.Deserialize(json, listType, SeedJsonOptions) as IList;

            if (entities is null || entities.Count == 0)
            {
                logger.LogWarning(
                    "Seed file '{FileName}' is empty or could not be deserialized. Skipping.",
                    Path.GetFileName(filePath));
                continue;
            }

            var genericSeedMethod = seedMethod.MakeGenericMethod(entityType);
            var task = (Task)genericSeedMethod.Invoke(this, [dbContext, entities, cancellationToken])!;
            await task;

            logger.LogInformation(
                "Seeded {Count} {TypeName} records from '{FileName}'.",
                entities.Count, entityType.Name, Path.GetFileName(filePath));
        }
    }

    /// <summary>
    /// Returns true if the entity type should be purged before seeding. Matches against
    /// both the fully qualified type name and the short class name so callers can use
    /// either form in configuration.
    /// </summary>
    private bool ShouldPurge(string fullyQualifiedName, string shortName) =>
        purgeBeforeSeed is not null &&
        (purgeBeforeSeed.Contains(fullyQualifiedName) || purgeBeforeSeed.Contains(shortName));

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

    /// <summary>
    /// Removes all rows for <typeparamref name="T"/> from the database, including
    /// soft-deleted rows. Uses <c>ExecuteDeleteAsync</c> on relational providers for
    /// efficiency; falls back to load-then-remove on non-relational providers (e.g.,
    /// in-memory databases used in tests).
    /// </summary>
    /// <remarks>
    /// This is destructive and unrecoverable outside of a database backup. Use with
    /// extreme caution and never enable it against a Production database.
    /// </remarks>
    protected async Task PurgeEntitiesAsync<T>(TContext dbContext, CancellationToken cancellationToken)
        where T : BaseEntity
    {
        if (dbContext.Database.IsRelational())
        {
            var deleted = await dbContext.Set<T>().IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
            logger.LogWarning(
                "Purged {Count} {TypeName} records before reseeding. PurgeBeforeSeed is destructive " +
                "and must never be enabled against a Production database.", deleted, typeof(T).Name);
        }
        else
        {
            var all = await dbContext.Set<T>().IgnoreQueryFilters().ToListAsync(cancellationToken);
            if (all.Count == 0) return;
            dbContext.Set<T>().RemoveRange(all);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Purged {Count} {TypeName} records before reseeding. PurgeBeforeSeed is destructive " +
                "and must never be enabled against a Production database.", all.Count, typeof(T).Name);
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
