using Microsoft.Identity.Web;

namespace Sample.Blazor.Infrastructure.Auth;

/// <summary>
/// Configures cookie + OpenID Connect authentication against Azure Entra (Azure AD)
/// using Microsoft Identity Web.
///
/// Required configuration keys (appsettings / env vars / user secrets):
///   AzureAd:TenantId
///   AzureAd:ClientId
///   AzureAd:ClientSecret   (store in user-secrets or a key vault in production)
///   AzureAd:CallbackPath   (defaults to /signin-oidc)
/// </summary>
public sealed class AzureEntraAuthConfigurator : IAuthProviderConfigurator
{
    public string ProviderName => "Azure Entra";
    public bool SupportsFederatedLogout => true;

    public void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMicrosoftIdentityWebAppAuthentication(
            configuration,
            configSectionName: "AzureAd");

        // Override the cookie paths so the UI components share the same login/logout routes
        // as the local OIDC configurator.
        services.Configure<CookieAuthenticationOptions>(
            CookieAuthenticationDefaults.AuthenticationScheme,
            options =>
            {
                options.LoginPath = "/account/login";
                options.LogoutPath = "/account/logout";
                options.AccessDeniedPath = "/account/access-denied";
            });
    }
}

