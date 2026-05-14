using CoreDesign.Data.Infrastructure;
using CoreDesign.Shared.ExtensionMethods;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sample.Api.Data;
using Sample.Api.WeatherForecasts.Models;

namespace Sample.Data.MigrationService;

public class SampleMigrationWorker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ILogger<SampleMigrationWorker> logger)
    : MigrationWorker<SampleDbContext>(serviceProvider, lifetime, logger)
{
    protected override async Task SeedAsync(SampleDbContext dbContext, CancellationToken cancellationToken)
    {
        var forecasts = "SeedData/Sample.Api.WeatherForecasts.Models.WeatherForecast.json"
            .LoadObjectFromJsonFile<List<WeatherForecast>>();

        if (forecasts is null or { Count: 0 })
        {
            logger.LogWarning("Seed file for WeatherForecast was empty or could not be read. Skipping.");
            return;
        }

        await SeedEntitiesAsync(dbContext, forecasts, cancellationToken);
        logger.LogInformation("Seeded {Count} WeatherForecast records.", forecasts.Count);
    }
}
