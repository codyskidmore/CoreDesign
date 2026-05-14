namespace CoreDesign.Identity.Server.Features.Jwks;

public class JwksResponse
{
    [JsonPropertyName("keys")]
    public List<JwkModel> Keys { get; set; } = [];
}
