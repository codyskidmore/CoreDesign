using System.Diagnostics;
using System.Reflection;
using Sample.Api.Data;
using CoreDesign.Shared.ExtensionMethods;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sample.Data.MigrationService;

public class MigrationService(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<MigrationService> logger) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("Migrating database", ActivityKind.Client);
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

            await EnsureDatabaseAsync(dbContext, cancellationToken);
            await RunMigrationAsync(dbContext, logger, cancellationToken);
            await SeedDatabaseAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    protected async Task SeedDatabaseAsync(SampleDbContext dbContext, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("Seeding database", ActivityKind.Client);
        try
        {
            string[] filePaths = Directory.GetFiles(@"SeedData", "*.json");

            var seedMethodInfo = typeof(MigrationService).GetMethod(nameof(SeedDataAsync),
                BindingFlags.Static | BindingFlags.NonPublic);

            if (seedMethodInfo == null)
            {
                logger.LogError("Could not find the SeedDataAsync method via reflection.");
                return;
            }

            foreach (var filePath in filePaths)
            {
                var qualifiedClassName = Path.GetFileNameWithoutExtension(filePath);
                var qualifiedName = $"{qualifiedClassName}, Sample.Api";
                var seedType = Type.GetType(qualifiedName);

                if (seedType == null)
                {
                    logger.LogWarning("Could not find type for seed file: {FileName}. Skipping.",
                        Path.GetFileName(filePath));
                    continue;
                }

                var genericSeedMethod = seedMethodInfo.MakeGenericMethod(typeof(SampleDbContext), seedType);
                var task = (Task)genericSeedMethod.Invoke(null,
                    new object[] { dbContext, filePath, cancellationToken })!;

                await task;

                logger.LogInformation("Successfully seeded data for type {TypeName}", seedType.Name);
            }
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            logger.LogError(ex, "An error occurred during database seeding.");
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    private static async Task EnsureDatabaseAsync(SampleDbContext dbContext, CancellationToken cancellationToken)
    {
        var dbCreator = dbContext.GetService<IRelationalDatabaseCreator>();

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            if (!await dbCreator.ExistsAsync(cancellationToken))
                await dbCreator.CreateAsync(cancellationToken);
        });
    }

    private static async Task RunMigrationAsync(SampleDbContext dbContext, ILogger<MigrationService> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Migrating database.");
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        });
        logger.LogInformation("Database migration completed successfully.");
    }

    private static async Task SeedDataAsync<TContext, T>(TContext dbContext, string fullPathToSeedJson,
        CancellationToken cancellationToken) where TContext : DbContext where T : class
    {
        var seedValues = fullPathToSeedJson.LoadObjectFromJsonFile<List<T>>();

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            foreach (var entity in seedValues)
            {
                var existing = await dbContext.Set<T>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Equals(entity), cancellationToken);
                if (existing != null)
                    continue;

                await dbContext.Set<T>().AddAsync(entity, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        });
    }
}
