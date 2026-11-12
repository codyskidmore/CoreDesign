using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoreDesign.Shared.Infrastructure;

/// <summary>Which environment settings file <c>AddAppSettings</c> chose, recorded for startup logging.</summary>
internal sealed record AppSettingsLoadInfo(string FileName, bool Found, string Directory);

/// <summary>Logs the settings file chosen by <c>AddAppSettings</c>, so a misnamed file is easy to spot.</summary>
internal sealed class AppSettingsStartupLogger(
    IEnumerable<AppSettingsLoadInfo> loads,
    ILogger<AppSettingsStartupLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var load in loads)
        {
            if (load.Found)
                logger.LogInformation("Loaded {SettingsFile}", load.FileName);
            else
                logger.LogInformation("{SettingsFile} not found in {Directory}; using appsettings.json only",
                    load.FileName, load.Directory);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
