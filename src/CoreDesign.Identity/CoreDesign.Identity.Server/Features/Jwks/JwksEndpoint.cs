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

public class JwkModel
{
    [JsonPropertyName("kty")]
    public string Kty { get; set; } = "RSA";

    [JsonPropertyName("use")]
    public string Use { get; set; } = "sig";

    [JsonPropertyName("alg")]
    public string Alg { get; set; } = "RS256";

    [JsonPropertyName("kid")]
    public string Kid { get; set; } = string.Empty;

    [JsonPropertyName("n")]
    public string N { get; set; } = string.Empty;

    [JsonPropertyName("e")]
    public string E { get; set; } = string.Empty;
}

public class JwksResponse
{
    [JsonPropertyName("keys")]
    public List<JwkModel> Keys { get; set; } = [];
}
