using Microsoft.Extensions.Configuration;
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
    /// <para>
    /// To clear specific tables before seeding, list entity type names (short or fully
    /// qualified) in <c>MigrationWorker:PurgeBeforeSeed</c> in appsettings or environment
    /// variables. The <paramref name="purgeBeforeSeed"/> parameter allows the same list to
    /// be supplied in code; both sources are merged.
    /// </para>
    /// <example>
    /// appsettings.json:
    /// <code>
    /// "MigrationWorker": {
    ///   "PurgeBeforeSeed": [ "SiteContent", "SiteSettings" ]
    /// }
    /// </code>
    /// Environment variable (Aspire / Docker):
    /// <code>
    /// MigrationWorker__PurgeBeforeSeed__0=SiteContent
    /// </code>
    /// </example>
    /// </summary>
    public static IHostApplicationBuilder AddMigrationWorker<TContext>(
        this IHostApplicationBuilder builder,
        string seedDirectory = "SeedData",
        IEnumerable<string>? purgeBeforeSeed = null)
        where TContext : DbContext
    {
        builder.Services.AddHostedService(sp =>
        {
            var config = sp.GetService<IConfiguration>();
            var fromConfig = config?
                .GetSection("MigrationWorker:PurgeBeforeSeed")
                .GetChildren()
                .Select(c => c.Value)
                .OfType<string>()
                .ToArray() ?? [];

            var merged = (purgeBeforeSeed ?? [])
                .Concat(fromConfig)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return new MigrationWorker<TContext>(
                sp,
                sp.GetRequiredService<IHostApplicationLifetime>(),
                sp.GetRequiredService<ILogger<MigrationWorker<TContext>>>(),
                seedDirectory,
                merged.Count > 0 ? merged : null);
        });

        return builder;
    }
}
