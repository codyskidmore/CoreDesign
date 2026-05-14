namespace CoreDesign.Identity.Server.Features.Jwks;

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
