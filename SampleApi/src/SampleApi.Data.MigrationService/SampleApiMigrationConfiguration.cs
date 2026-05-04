using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SampleApi.Data.MigrationService;

public static class SampleApiMigrationConfiguration
{
    public static IServiceCollection AddConfiguration(this IHostApplicationBuilder builder)
    {
        return builder.Services;
    }
}
