using System.Security.Cryptography;
using CoreDesign.Identity.Server.Features.Authorize;
using CoreDesign.Identity.Server.Features.AuthLogin;
using CoreDesign.Identity.Server.Features.GetToken;
using CoreDesign.Identity.Server.Features.Jwks;
using CoreDesign.Identity.Server.Features.OidcDiscovery;
using CoreDesign.Identity.Server.Features.Token;
using CoreDesign.Identity.Server.Features.UserInfo;

namespace CoreDesign.Identity.Server;

public static class IdentityExtensions
{
    private const string CorsPolicyName = "coredesign-identity";

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

        var rsa = GetOrCreateDevKey(options.KeyId);
        var rsaKey = new RsaSecurityKey(rsa) { KeyId = options.KeyId };
        services.AddSingleton(rsaKey);
        services.AddSingleton(new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256));

        services.AddCors(cors =>
            cors.AddPolicy(CorsPolicyName, policy =>
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        return services;
    }

    private static RSA GetOrCreateDevKey(string keyId)
    {
        var keyDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "coredesign-identity");
        var keyPath = Path.Combine(keyDir, $"{keyId}.pem");

        if (File.Exists(keyPath))
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(keyPath));
            return rsa;
        }

        Directory.CreateDirectory(keyDir);
        var newKey = RSA.Create(2048);
        try
        {
            File.WriteAllText(keyPath, newKey.ExportRSAPrivateKeyPem());
        }
        catch (IOException)
        {
            if (File.Exists(keyPath))
            {
                newKey.Dispose();
                newKey = RSA.Create();
                newKey.ImportFromPem(File.ReadAllText(keyPath));
            }
        }
        return newKey;
    }

    public static IServiceCollection AddJsonFileIdentityStore(
        this IServiceCollection services,
        string filePath)
    {
        services.AddSingleton<IIdentityStore>(new JsonFileIdentityStore(filePath));
        return services;
    }

    public static IServiceCollection AddJsonFileClientStore(
        this IServiceCollection services,
        string filePath)
    {
        services.AddSingleton<IClientStore>(new JsonFileClientStore(filePath));
        return services;
    }

    public static WebApplication UseIdentityServerCors(this WebApplication app)
    {
        app.UseCors(CorsPolicyName);
        return app;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        OidcDiscoveryEndpoint.Map(app);
        JwksEndpoint.Map(app);
        AuthorizeEndpoint.Map(app);
        TokenEndpoint.Map(app);
        UserInfoEndpoint.Map(app);
        GetTokenEndpoint.Map(app);
        AuthLoginEndpoint.Map(app);
        return app;
    }
}
