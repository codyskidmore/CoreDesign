using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CoreDesign.Shared.Infrastructure;

public static class Configuration
{
    private const string BaseSettingsFile = "appsettings.json";
    private const string SettingsFilePrefix = "appsettings";

    public static IHostApplicationBuilder AddDatabaseConfiguration(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(nameof(DatabaseOptions)));
        return builder;
    }

    /// <summary>
    /// Loads <c>appsettings.json</c> and <c>appsettings.{name}.json</c> from the application's output
    /// folder, for Aspire AppHost projects. See the <see cref="IHostApplicationBuilder"/> overload.
    /// </summary>
    public static IDistributedApplicationBuilder AddAppSettings(
        this IDistributedApplicationBuilder builder,
        Action<AppSettingsOptions>? configure = null)
    {
        AddAppSettings(builder.Configuration, builder.Environment, builder.Services, configure);
        return builder;
    }

    /// <summary>
    /// Loads <c>appsettings.json</c> and <c>appsettings.{name}.json</c> from the application's output
    /// folder, which is where settings files linked from a shared folder are copied.
    /// </summary>
    /// <remarks>
    /// <para>The name is <see cref="AppSettingsOptions.SettingsName"/> when set, otherwise the
    /// <c>AppSettings:Name</c> configuration value (for example the environment variable
    /// <c>AppSettings__Name</c>), otherwise the host's environment name. The named file is optional.</para>
    /// <para>The files are placed directly after the host's own <c>appsettings</c> files, so user secrets,
    /// environment variables and command-line arguments still override them, as in any .NET host.
    /// The loaded file name is logged at Information level when the host starts.</para>
    /// </remarks>
    public static IHostApplicationBuilder AddAppSettings(
        this IHostApplicationBuilder builder,
        Action<AppSettingsOptions>? configure = null)
    {
        AddAppSettings(builder.Configuration, builder.Environment, builder.Services, configure);
        return builder;
    }

    private static void AddAppSettings(
        IConfigurationManager configuration,
        IHostEnvironment env,
        IServiceCollection services,
        Action<AppSettingsOptions>? configure)
    {
        var options = new AppSettingsOptions();
        configure?.Invoke(options);

        var settingsName = options.SettingsName
                           ?? configuration[AppSettingsOptions.NameKey]
                           ?? env.EnvironmentName;
        var settingsFile = $"{SettingsFilePrefix}.{settingsName}.json";

        var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;

        // Kept for compatibility: JSON files an app adds afterwards resolve from the same folder.
        configuration.SetBasePath(baseDirectory);

        var fileProvider = new PhysicalFileProvider(baseDirectory);
        IConfigurationSource[] settingsSources =
        [
            JsonSource(fileProvider, BaseSettingsFile, optional: false, reloadOnChange: true),
            JsonSource(fileProvider, settingsFile, optional: true, reloadOnChange: false),
        ];

        var sources = ((IConfigurationBuilder)configuration).Sources;
        var insertAt = FindSettingsInsertIndex(sources);
        for (var i = 0; i < settingsSources.Length; i++)
            sources.Insert(insertAt + i, settingsSources[i]);

        services.AddSingleton(new AppSettingsLoadInfo(settingsFile, File.Exists(Path.Combine(baseDirectory, settingsFile)), baseDirectory));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, AppSettingsStartupLogger>());
    }

    private static JsonConfigurationSource JsonSource(IFileProvider provider, string path, bool optional, bool reloadOnChange) =>
        new() { FileProvider = provider, Path = path, Optional = optional, ReloadOnChange = reloadOnChange };

    /// <summary>
    /// Returns the position just after the host's own appsettings files. When there are none, the
    /// position of the first override source (user secrets, unprefixed environment variables or
    /// command-line arguments); when there are none of those either, the end of the list.
    /// </summary>
    internal static int FindSettingsInsertIndex(IList<IConfigurationSource> sources)
    {
        var lastHostSettings = -1;
        for (var i = 0; i < sources.Count; i++)
        {
            if (sources[i] is JsonConfigurationSource { Path: { } path }
                && path.StartsWith(SettingsFilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                lastHostSettings = i;
            }
        }

        if (lastHostSettings >= 0)
            return lastHostSettings + 1;

        for (var i = 0; i < sources.Count; i++)
        {
            if (IsOverrideSource(sources[i]))
                return i;
        }

        return sources.Count;
    }

    private static bool IsOverrideSource(IConfigurationSource source) => source switch
    {
        JsonConfigurationSource { Path: "secrets.json" } => true,
        Microsoft.Extensions.Configuration.EnvironmentVariables.EnvironmentVariablesConfigurationSource env
            => string.IsNullOrEmpty(env.Prefix),
        Microsoft.Extensions.Configuration.CommandLine.CommandLineConfigurationSource => true,
        _ => false,
    };
}
