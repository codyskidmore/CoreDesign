using System.Security.Cryptography;
using CoreDesign.Identity.Server.Features.GetToken;
using CoreDesign.Identity.Server.Features.Jwks;
using CoreDesign.Identity.Server.Features.OidcDiscovery;
using CoreDesign.Identity.Server.Features.Token;
using CoreDesign.Identity.Server.Features.UserInfo;
using Microsoft.IdentityModel.Tokens;

namespace CoreDesign.Identity.Server;

public static class IdentityExtensions
{
    public static IServiceCollection AddIdentityServer(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "CoreDesign:Identity",
        Action<IdentityOptions>? configure = null)
    {
        var options = new IdentityOptions();
        configuration.GetSection(sectionName).Bind(options);
        configure?.Invoke(options);
        services.AddSingleton(options);

        var rsa = RSA.Create(2048);
        var rsaKey = new RsaSecurityKey(rsa) { KeyId = options.KeyId };
        services.AddSingleton(rsaKey);
        services.AddSingleton(new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256));

        return services;
    }

    public static IServiceCollection AddJsonFileIdentityStore(
        this IServiceCollection services,
        string filePath)
    {
        services.AddSingleton<IIdentityStore>(new JsonFileIdentityStore(filePath));
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        OidcDiscoveryEndpoint.Map(app);
        JwksEndpoint.Map(app);
        TokenEndpoint.Map(app);
        UserInfoEndpoint.Map(app);
        GetTokenEndpoint.Map(app);
        return app;
    }
}
