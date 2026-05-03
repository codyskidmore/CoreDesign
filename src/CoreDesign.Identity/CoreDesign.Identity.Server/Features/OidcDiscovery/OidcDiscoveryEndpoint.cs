using System.Text.Json.Serialization;

namespace CoreDesign.Identity.Server.Features.OidcDiscovery;

public static class OidcDiscoveryEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/.well-known/openid-configuration", Handle)
           .WithName("OidcDiscovery")
           .WithTags("OIDC");

    private static IResult Handle(HttpContext ctx, IdentityOptions options)
    {
        var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
        return Results.Ok(new OidcDiscovery
        {
            Issuer = options.Issuer,
            AuthorizationEndpoint = $"{baseUrl}/connect/authorize",
            TokenEndpoint = $"{baseUrl}/connect/token",
            UserinfoEndpoint = $"{baseUrl}/connect/userinfo",
            JwksUri = $"{baseUrl}/.well-known/jwks.json",
            ResponseTypesSupported = ["code", "token", "id_token"],
            SubjectTypesSupported = ["public"],
            IdTokenSigningAlgValuesSupported = ["RS256"],
            ScopesSupported = ["openid", "profile", "email"],
            TokenEndpointAuthMethodsSupported = ["none"],
            GrantTypesSupported = ["password"],
            ClaimsSupported = ["sub", "iss", "aud", "exp", "iat", "jti", "email", "name", "given_name", "family_name", "roles"]
        });
    }
}

public class OidcDiscovery
{
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("userinfo_endpoint")]
    public string UserinfoEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("jwks_uri")]
    public string JwksUri { get; set; } = string.Empty;

    [JsonPropertyName("response_types_supported")]
    public string[] ResponseTypesSupported { get; set; } = [];

    [JsonPropertyName("subject_types_supported")]
    public string[] SubjectTypesSupported { get; set; } = [];

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public string[] IdTokenSigningAlgValuesSupported { get; set; } = [];

    [JsonPropertyName("scopes_supported")]
    public string[] ScopesSupported { get; set; } = [];

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public string[] TokenEndpointAuthMethodsSupported { get; set; } = [];

    [JsonPropertyName("grant_types_supported")]
    public string[] GrantTypesSupported { get; set; } = [];

    [JsonPropertyName("claims_supported")]
    public string[] ClaimsSupported { get; set; } = [];
}
