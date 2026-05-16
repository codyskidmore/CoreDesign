using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace CoreDesign.Identity.Client;

public static class IdentityClientExtensions
{
    public static IServiceCollection AddIdentityClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IdentityApiOptions>(configuration.GetSection("IdentityApi"));

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

    public static WebApplication UseLocalBearerTokenInjection(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            app.UseMiddleware<BearerTokenInjectionMiddleware>();

        return app;
    }

}
