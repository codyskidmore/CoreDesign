using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoreDesign.Shared.Infrastructure;

public static class Configuration
{
    public static IHostApplicationBuilder AddDatabaseConfiguration(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(nameof(DatabaseOptions)));
        return builder;
    }

    public static IDistributedApplicationBuilder AddAppSettings(this IDistributedApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var env = builder.Environment;

        AddAppSettings(configuration, env);

        return builder;
    }

    public static IHostApplicationBuilder AddAppSettings(this IHostApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var env = builder.Environment;

        AddAppSettings(configuration, env);

        return builder;
    }

    private static void AddAppSettings(
        IConfigurationManager configuration,
        IHostEnvironment env)
    {
        configuration
            .SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "")
            .AddJsonFile("appsettings.json", false, true);

        configuration.AddJsonFile($"appsettings.{env.EnvironmentName}.json", true);
    }
}