namespace Sample.Blazor.Infrastructure.Auth;

/// <summary>
/// Configures cookie + OpenID Connect authentication against the local
/// SampleApi.Identity.Api identity server.
///
/// Authority resolution order (first match wins):
///   1. Blazor:Oidc:Authority (appsettings / env vars / user secrets)
///   2. services:SampleApiIdentityApi:https:0 (Aspire-injected, normalized)
///   3. services__SampleApiIdentityApi__https__0 (Aspire-injected raw)
///   4. Connection string "SampleApiIdentityApi"
///   5. IdentityApi:BaseUrl (appsettings file, defaults to Aspire service name)
///
/// Client configuration:
///   Blazor:Oidc:ClientId (defaults to "sample-blazor")
///   Blazor:Oidc:Scopes (defaults to ["openid","profile","email"])
/// </summary>
public sealed class LocalOidcAuthConfigurator : IAuthProviderConfigurator
{
    public string ProviderName => "Local Identity Server";

    public void Configure(IServiceCollection services, IConfiguration configuration)
    {
        // Resolve the authority. Preference order:
        // 1. Explicitly configured value (Blazor:Oidc:Authority)
        // 2. Aspire-injected service URL variations:
        //    - services:SampleApiIdentityApi:https:0 (IConfiguration normalized form)
        //    - services__SampleApiIdentityApi__https__0 (raw env var form)
        // 3. Developer fallback from appsettings.Development.json (IdentityApi:BaseUrl)
        var authority =
            configuration["Blazor:Oidc:Authority"]
            ?? configuration["services:SampleApiIdentityApi:https:0"]
            ?? configuration["services__SampleApiIdentityApi__https__0"]
            ?? configuration.GetConnectionString("SampleApiIdentityApi")
            ?? configuration["IdentityApi:BaseUrl"]
            ?? throw new InvalidOperationException(
                "OIDC authority is not configured. Set 'Blazor:Oidc:Authority', " +
                "or run the app through Aspire with the identity API referenced, " +
                "or set 'IdentityApi:BaseUrl' in appsettings.Development.json");

        var clientId = configuration["Blazor:Oidc:ClientId"] ?? "sample-blazor";
        var scopes = configuration.GetSection("Blazor:Oidc:Scopes").Get<string[]>()
                     ?? ["openid", "profile", "email"];

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/account/login";
                options.LogoutPath = "/account/logout";
                options.AccessDeniedPath = "/account/access-denied";
            })
            .AddOpenIdConnect(options =>
            {
                options.Authority = authority;
                options.ClientId = clientId;

                // Use Authorization Code flow with PKCE (no client secret required)
                options.ResponseType = "code";
                options.UsePkce = true;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;

                // Disable HTTPS metadata requirement for local development.
                // Set to true in production environments.
                options.RequireHttpsMetadata = false;

                options.Scope.Clear();
                foreach (var scope in scopes)
                    options.Scope.Add(scope);
            });
    }
}

