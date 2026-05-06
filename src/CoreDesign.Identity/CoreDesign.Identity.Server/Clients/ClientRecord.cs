namespace CoreDesign.Identity.Server.Clients;

public sealed class ClientRecord
{
    public string ClientId { get; init; } = string.Empty;
    public string? ClientSecret { get; init; }
    public string TokenEndpointAuthMethod { get; init; } = "none";
    public List<string> AllowedGrantTypes { get; init; } = [];
    public List<string> AllowedRedirectUris { get; init; } = [];
    public List<string> AllowedPostLogoutRedirectUris { get; init; } = [];
    public List<string> AllowedScopes { get; init; } = [];
    public bool RequirePkce { get; init; } = true;
}
