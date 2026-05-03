using System.Text.Json.Serialization;

namespace CoreDesign.Identity.Server;

public class OidcError
{
    [JsonPropertyName("error")]
    public string Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    public OidcError(string error, string? errorDescription = null)
    {
        Error = error;
        ErrorDescription = errorDescription;
    }
}
