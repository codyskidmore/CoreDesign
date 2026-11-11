using System.Collections.Concurrent;
using CoreDesign.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration.Memory;

namespace CoreDesign.Shared.Tests.Infrastructure;

/// <summary>
/// AddAppSettings reads from the folder that contains CoreDesign.Shared.dll, which for these tests is
/// the test output folder. Each test uses its own environment name and keys so files never collide.
/// </summary>
[Collection(nameof(AppSettingsTests))]
public class AppSettingsTests
{
    private const string Key = "AppSettingsTest:Key";
    private static readonly string OutputFolder = AppContext.BaseDirectory;

    static AppSettingsTests() =>
        File.WriteAllText(Path.Combine(OutputFolder, "appsettings.json"), """{ "AppSettingsTest": { "Key": "base" } }""");

    [Fact]
    public void Default_UsesEnvironmentNameFile()
    {
        WriteSettings("EnvDefault", "env-default");

        var builder = CreateBuilder("EnvDefault");
        builder.AddAppSettings();

        Assert.Equal("env-default", builder.Configuration[Key]);
    }

    [Fact]
    public void SettingsName_SelectsNamedFile_AndKeepsEnvironment()
    {
        WriteSettings("Development", "dev");
        WriteSettings("LOCAL", "local");

        var builder = CreateBuilder(Environments.Development);
        builder.AddAppSettings(o => o.SettingsName = "LOCAL");

        Assert.Equal("local", builder.Configuration[Key]);
        Assert.True(builder.Environment.IsDevelopment());
    }

    [Fact]
    public void NameKey_FromCommandLine_SelectsNamedFile()
    {
        WriteSettings("CLINAME", "from-cli-name");

        var builder = CreateBuilder("Development", $"--{AppSettingsOptions.NameKey}=CLINAME");
        builder.AddAppSettings();

        Assert.Equal("from-cli-name", builder.Configuration[Key]);
    }

    [Fact]
    public void NameKey_FromEnvironmentVariable_SelectsNamedFile()
    {
        WriteSettings("ENVVARNAME", "from-env-name");
        const string variable = "AppSettings__Name";
        Environment.SetEnvironmentVariable(variable, "ENVVARNAME");
        try
        {
            var builder = CreateBuilder("Development");
            builder.AddAppSettings();

            Assert.Equal("from-env-name", builder.Configuration[Key]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void SettingsName_WinsOverNameKey()
    {
        WriteSettings("WINNER", "winner");
        WriteSettings("LOSER", "loser");

        var builder = CreateBuilder("Development", $"--{AppSettingsOptions.NameKey}=LOSER");
        builder.AddAppSettings(o => o.SettingsName = "WINNER");

        Assert.Equal("winner", builder.Configuration[Key]);
    }

    [Fact]
    public void CustomEnvironmentName_LoadsItsFile()
    {
        WriteSettings("DEVAZ", "azure-dev");

        var builder = CreateBuilder("DEVAZ");
        builder.AddAppSettings();

        Assert.Equal("azure-dev", builder.Configuration[Key]);
    }

    [Fact]
    public void MissingNamedFile_IsNotAnError()
    {
        var builder = CreateBuilder("NOFILE");
        builder.AddAppSettings();

        Assert.Equal("base", builder.Configuration[Key]);
    }

    [Fact]
    public void EnvironmentVariables_OverrideJson()
    {
        const string key = "AppSettingsTest:EnvOverride";
        WriteSettings("ENVOVERRIDE", "json", key);
        const string variable = "AppSettingsTest__EnvOverride";
        Environment.SetEnvironmentVariable(variable, "env");
        try
        {
            var builder = CreateBuilder("ENVOVERRIDE");
            builder.AddAppSettings();

            Assert.Equal("env", builder.Configuration[key]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void CommandLine_OverridesJson()
    {
        const string key = "AppSettingsTest:CliOverride";
        WriteSettings("CLIOVERRIDE", "json", key);

        var builder = CreateBuilder("CLIOVERRIDE", $"--{key}=cli");
        builder.AddAppSettings();

        Assert.Equal("cli", builder.Configuration[key]);
    }

    [Fact]
    public void NamedFile_OverridesBaseFile()
    {
        WriteSettings("OVERBASE", "named");

        var builder = CreateBuilder("OVERBASE");
        builder.AddAppSettings();

        Assert.Equal("named", builder.Configuration[Key]);
    }

    [Fact]
    public async Task Startup_LogsLoadedFile()
    {
        WriteSettings("LOGGED", "logged");
        var logs = new ListLoggerProvider();

        var builder = CreateBuilder("LOGGED");
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.AddAppSettings();

        using var host = builder.Build();
        await host.StartAsync();
        await host.StopAsync();

        Assert.Contains(logs.Messages, m => m == "Loaded appsettings.LOGGED.json");
    }

    [Fact]
    public async Task Startup_LogsMissingFile()
    {
        var logs = new ListLoggerProvider();

        var builder = CreateBuilder("NOTTHERE");
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.AddAppSettings();

        using var host = builder.Build();
        await host.StartAsync();
        await host.StopAsync();

        Assert.Contains(logs.Messages, m => m.StartsWith("appsettings.NOTTHERE.json not found"));
    }

    [Fact]
    public void FindSettingsInsertIndex_PlacesAfterHostSettings_BeforeOverrides()
    {
        var sources = new List<IConfigurationSource>
        {
            new EnvironmentVariablesConfigurationSource { Prefix = "DOTNET_" },
            new JsonConfigurationSource { Path = "appsettings.json" },
            new JsonConfigurationSource { Path = "appsettings.Development.json" },
            new JsonConfigurationSource { Path = "secrets.json" },
            new EnvironmentVariablesConfigurationSource(),
        };

        Assert.Equal(3, Configuration.FindSettingsInsertIndex(sources));
    }

    [Fact]
    public void FindSettingsInsertIndex_WithoutHostSettings_PlacesBeforeFirstOverride()
    {
        var sources = new List<IConfigurationSource>
        {
            new EnvironmentVariablesConfigurationSource { Prefix = "DOTNET_" },
            new MemoryConfigurationSource(),
            new EnvironmentVariablesConfigurationSource(),
        };

        Assert.Equal(2, Configuration.FindSettingsInsertIndex(sources));
    }

    [Fact]
    public void FindSettingsInsertIndex_WithNoMarkers_Appends()
    {
        var sources = new List<IConfigurationSource> { new MemoryConfigurationSource() };

        Assert.Equal(1, Configuration.FindSettingsInsertIndex(sources));
    }

    private static HostApplicationBuilder CreateBuilder(string environment, params string[] args) =>
        Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environment,
            ContentRootPath = OutputFolder,
            Args = args,
        });

    private static void WriteSettings(string name, string value, string key = Key)
    {
        var parts = key.Split(':');
        var json = $$"""{ "{{parts[0]}}": { "{{parts[1]}}": "{{value}}" } }""";
        File.WriteAllText(Path.Combine(OutputFolder, $"appsettings.{name}.json"), json);
    }

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyList<string> Messages => _messages.ToArray();

        public ILogger CreateLogger(string categoryName) => new ListLogger(_messages);

        public void Dispose() { }

        private sealed class ListLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception));
        }
    }
}
