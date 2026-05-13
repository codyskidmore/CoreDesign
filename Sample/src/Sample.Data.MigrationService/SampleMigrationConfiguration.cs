using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sample.Data.MigrationService;

public static class SampleMigrationConfiguration
{
    public static IServiceCollection AddConfiguration(this IHostApplicationBuilder builder)
    {
        return builder.Services;
    }
}
