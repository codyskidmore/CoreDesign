using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Microsoft.ApplicationInsights.Extensibility;

namespace SampleApi.Api.Infrastructure;

public static class SerilogExtensions
{
    public static void UseApplicationSerilog(this ConfigureHostBuilder host)
    {
        host.UseSerilog((context, services, config) =>
            config
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.ApplicationInsights(
                    services.GetRequiredService<TelemetryConfiguration>(),
                    new TraceTelemetryConverter()));
    }
}
