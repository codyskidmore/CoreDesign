using Microsoft.AspNetCore.Hosting;

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

        app.MapGet("/", (IWebHostEnvironment env) =>
        {
            var html = TemplateLoader.Load("landing.html", env);
            return Results.Content(html, "text/html; charset=utf-8");
        }).AllowAnonymous();

        return app;
    }
}
