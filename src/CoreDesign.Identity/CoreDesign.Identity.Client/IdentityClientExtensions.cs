using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace CoreDesign.Identity.Client;

public static class IdentityClientExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication against the CoreDesign identity server, the
    /// <see cref="IdentityApiClient"/> used for local token injection, and permission-based authorization.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration containing the <c>IdentityApi</c> and <c>CoreDesign:Identity</c> sections.</param>
    /// <param name="configure">Optional overrides applied after the <c>IdentityApi</c> section is bound,
    /// for example <c>o => o.EnableLocalTokenInjection = true</c>.</param>
    public static IServiceCollection AddIdentityClient(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IdentityApiOptions>? configure = null)
    {
        services.Configure<IdentityApiOptions>(configuration.GetSection(IdentityApiOptions.SectionName));
        if (configure is not null)
            services.PostConfigure(configure);

        services.AddHttpClient("coredesign-identity", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<IdentityApiOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<IdentityApiClient>();

        var identityBaseUrl = configuration["IdentityApi:BaseUrl"]!;
        var jwtIssuer = configuration["CoreDesign:IdentityWebHost:Issuer"]
                        ?? configuration["CoreDesign:Identity:Issuer"]
                        ?? throw new InvalidOperationException("Identity issuer configuration is missing. Set CoreDesign:IdentityWebHost:Issuer.");
        var jwtAudience = configuration["CoreDesign:IdentityWebHost:Audience"]
                          ?? configuration["CoreDesign:Identity:Audience"]
                          ?? throw new InvalidOperationException("Identity audience configuration is missing. Set CoreDesign:IdentityWebHost:Audience.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MetadataAddress = $"{identityBaseUrl}/.well-known/openid-configuration";
                options.RequireHttpsMetadata = false;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    NameClaimType = "email"
                };
            });

        services.AddPermissionAuthorization();

        return services;
    }

    /// <summary>
    /// Signs tokenless requests from localhost in as the configured <c>IdentityApi</c> user, so local
    /// tools such as Scalar and curl work without a sign-in. Controlled by
    /// <see cref="IdentityApiOptions.EnableLocalTokenInjection"/>; when that is not set, it is on in
    /// Development only. Requests from other addresses and requests that already carry an
    /// <c>Authorization</c> header are never changed.
    /// </summary>
    public static WebApplication UseLocalBearerTokenInjection(this WebApplication app)
    {
        var setting = app.Services.GetRequiredService<IOptions<IdentityApiOptions>>().Value.EnableLocalTokenInjection
                      ?? app.Configuration.GetValue<bool?>(IdentityApiOptions.EnableLocalTokenInjectionKey);

        if (!LocalTokenInjection.IsEnabled(setting, app.Environment))
            return app;

        if (app.Services.GetService<IdentityApiClient>() is null)
        {
            app.Logger.LogWarning(
                "Local bearer token injection is enabled but AddIdentityClient was not called, so it is skipped.");
            return app;
        }

        if (!app.Environment.IsDevelopment())
        {
            app.Logger.LogWarning(
                "Local bearer token injection is ON in the {Environment} environment. Tokenless requests from " +
                "localhost are signed in as the configured IdentityApi user. Behind a reverse proxy on the same " +
                "machine, every request can appear to come from localhost unless forwarded headers are configured. " +
                "Set {Key} to false in deployed environments.",
                app.Environment.EnvironmentName,
                IdentityApiOptions.EnableLocalTokenInjectionKey);
        }

        app.UseMiddleware<BearerTokenInjectionMiddleware>();
        return app;
    }

}
