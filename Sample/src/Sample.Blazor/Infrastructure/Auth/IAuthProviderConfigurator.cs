namespace Sample.Blazor.Infrastructure.Auth;

/// <summary>
/// Abstraction that lets a concrete auth provider register its services into the DI
/// container. Implementations are selected at startup via the "Blazor:AuthProvider"
/// configuration key ("Local" or "AzureEntra").
/// </summary>
public interface IAuthProviderConfigurator
{
    /// <summary>Human-readable name shown in the UI.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Indicates whether the provider supports federated sign-out via the OIDC
    /// end-session endpoint.
    /// </summary>
    bool SupportsFederatedLogout { get; }

    /// <summary>Configure authentication and related services.</summary>
    void Configure(IServiceCollection services, IConfiguration configuration);
}

