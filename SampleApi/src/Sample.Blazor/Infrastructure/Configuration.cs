using SampleApi.Aspire.ServiceDefaults;
using Serilog;

namespace Sample.Blazor.Infrastructure;

public static class Configuration
{
    public static WebApplicationBuilder AddConfiguration(this WebApplicationBuilder builder)
    {
        builder.AddAspireServiceDefaults();

        // Select and register the auth provider based on configuration.
        // Change "Blazor:AuthProvider" to "AzureEntra" for Azure Entra authentication.
        builder.AddAuthProviderConfiguration();

        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();

        // Razor components with Interactive Server rendering
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Typed HttpClient for SampleApi.Api with Aspire service discovery
        builder.Services.AddTransient<BearerTokenHandler>();
        builder.Services.AddHttpClient<SampleApiClient>(client =>
                client.BaseAddress = new Uri("https://SampleApiApi"))
            .AddHttpMessageHandler<BearerTokenHandler>();

        builder.Host.UseSerilog((context, services, loggerConfig) =>
            loggerConfig.ReadFrom.Configuration(context.Configuration));

        return builder;
    }

    private static WebApplicationBuilder AddAuthProviderConfiguration(
        this WebApplicationBuilder builder)
    {
        var providerKey = builder.Configuration["Blazor:AuthProvider"] ?? "Local";

        IAuthProviderConfigurator configurator =
            providerKey.Equals("AzureEntra", StringComparison.OrdinalIgnoreCase)
                ? new AzureEntraAuthConfigurator()
                : new LocalOidcAuthConfigurator();

        // Register the active configurator so components can display the provider name.
        builder.Services.AddSingleton(configurator);

        configurator.Configure(builder.Services, builder.Configuration);

        return builder;
    }
}

