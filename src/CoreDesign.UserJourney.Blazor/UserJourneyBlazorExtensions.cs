using CoreDesign.UserJourney.Blazor;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

public static class UserJourneyBlazorServiceCollectionExtensions
{
    /// <summary>
    /// Adds CoreDesign.UserJourney with Blazor Server support: links Blazor clicks and in-app navigation
    /// to the signed-in user. Call it before <c>AddServiceDefaults()</c> or <c>UseAzureMonitor()</c> so it
    /// runs before their exporters, and add <c>app.UseUserJourney()</c> after authentication.
    /// </summary>
    public static IServiceCollection AddUserJourneyBlazor(this IServiceCollection services, Action<CoreDesign.UserJourney.UserJourneyOptions>? configure = null)
    {
        services.AddUserJourney(configure);

        services.TryAddSingleton<CircuitUserRegistry>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<CircuitHandler, UserJourneyCircuitHandler>());

        services.AddOpenTelemetry().WithTracing(tracing => tracing
            .AddSource(BlazorTraceTags.ComponentsActivitySource, BlazorTraceTags.ServerActivitySource)
            .AddProcessor(sp => new UserJourneyBlazorProcessor(sp.GetRequiredService<CircuitUserRegistry>())));

        return services;
    }
}
