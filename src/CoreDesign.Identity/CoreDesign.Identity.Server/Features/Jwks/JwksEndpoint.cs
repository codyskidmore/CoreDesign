namespace CoreDesign.Identity.Server.Features.Jwks;

public static class JwksEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/.well-known/jwks.json", Handle)
           .WithName("Jwks")
           .WithTags("OIDC");

    private static IResult Handle(RsaSecurityKey rsaKey)
    {
        var rsaParams = rsaKey.Rsa.ExportParameters(false);
        return Results.Ok(new JwksResponse
        {
            Keys =
            [
                new JwkModel
                {
                    Kid = rsaKey.KeyId,
                    N = Base64UrlEncoder.Encode(rsaParams.Modulus!),
                    E = Base64UrlEncoder.Encode(rsaParams.Exponent!)
                }
            ]
        });
    }
}
