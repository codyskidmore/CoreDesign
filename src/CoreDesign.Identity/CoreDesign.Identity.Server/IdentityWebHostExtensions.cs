namespace CoreDesign.Identity.Server;

public static class IdentityWebHostExtensions
{
    public static IServiceCollection AddIdentityServerWebHost(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "CoreDesign:IdentityWebHost")
    {
        var options = new IdentityWebHostOptions();
        configuration.GetSection(sectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.Issuer))
            throw new InvalidOperationException($"Missing configuration value: {sectionName}:Issuer");

        if (string.IsNullOrWhiteSpace(options.Audience))
            throw new InvalidOperationException($"Missing configuration value: {sectionName}:Audience");

        services.AddSingleton(options);

        services.AddIdentityServer(configuration, configure: identityOptions =>
        {
            identityOptions.Issuer = options.Issuer;
            identityOptions.Audience = options.Audience;
            identityOptions.KeyId = options.KeyId;
            identityOptions.TokenLifetimeHours = options.TokenLifetimeHours;
        });

        services.AddJsonFileIdentityStore(options.IdentitiesFilePath);
        services.AddJsonFileClientStore(options.ClientsFilePath);

        return services;
    }

    public static WebApplication MapIdentityServerWebHost(this WebApplication app)
    {
        app.UseIdentityServerCors();
        app.MapIdentityEndpoints();

        app.MapGet("/", () =>
            Results.Content(
                """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>CoreDesign Identity Web Host</title>
</head>
<body>
  <h1>CoreDesign Identity Web Host</h1>
  <p>Development identity provider is running.</p>
  <ul>
    <li><a href="/.well-known/openid-configuration">OpenID Connect discovery</a></li>
    <li><a href="/.well-known/jwks.json">JWKS</a></li>
  </ul>
</body>
</html>
""",
                "text/html; charset=utf-8"));

        return app;
    }
}

