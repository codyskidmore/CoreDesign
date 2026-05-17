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
            ResponseTypesSupported = ["code"],
            SubjectTypesSupported = ["public"],
            IdTokenSigningAlgValuesSupported = ["RS256"],
            ScopesSupported = ["openid", "profile", "email"],
            TokenEndpointAuthMethodsSupported = ["none", "client_secret_post"],
            GrantTypesSupported = ["authorization_code", "password"],
            CodeChallengeMethodsSupported = ["S256"],
            ClaimsSupported = ["sub", "iss", "aud", "exp", "iat", "jti", "email", "preferred_username", "name", "given_name", "family_name", "oid", "roles"]
        });
    }
}
