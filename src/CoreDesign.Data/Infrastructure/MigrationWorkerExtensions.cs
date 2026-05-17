using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoreDesign.Data.Infrastructure;

public static class MigrationWorkerExtensions
{
    /// <summary>
    /// Registers <see cref="MigrationWorker{TContext}"/> as a hosted service. The worker
    /// runs EF Core migrations and seeds from JSON files in <paramref name="seedDirectory"/>
    /// (default: <c>SeedData</c>) on startup, then stops the host.
    /// </summary>
    public static IHostApplicationBuilder AddMigrationWorker<TContext>(
        this IHostApplicationBuilder builder,
        string seedDirectory = "SeedData")
        where TContext : DbContext
    {
        builder.Services.AddHostedService(sp =>
            new MigrationWorker<TContext>(
                sp,
                sp.GetRequiredService<IHostApplicationLifetime>(),
                sp.GetRequiredService<ILogger<MigrationWorker<TContext>>>(),
                seedDirectory));

        return builder;
    }
}
