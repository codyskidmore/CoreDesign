using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoreDesign.Identity.Client;

public static class IdentityClientExtensions
{
    public static IServiceCollection AddLocalIdentityAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IdentityApiOptions>(configuration.GetSection("IdentityApi"));

        services.AddHttpClient("dcne-identity", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<IdentityApiOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<IdentityApiClient>();

        var identityBaseUrl = configuration["IdentityApi:BaseUrl"]!;
        var jwtIssuer = configuration["CoreDesign:Identity:Issuer"]!;
        var jwtAudience = configuration["CoreDesign:Identity:Audience"]!;

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
                    RoleClaimType = "roles",
                    NameClaimType = "email"
                };
            });

        return services;
    }

    public static WebApplication UseLocalBearerTokenInjection(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            app.UseMiddleware<BearerTokenInjectionMiddleware>();

        return app;
    }
}
