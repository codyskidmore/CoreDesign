namespace CoreDesign.Identity.Server;

public sealed class IdentityWebHostOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string KeyId { get; set; } = "coredesign-dev-signing-key";
    public int TokenLifetimeHours { get; set; } = 8;
    public string IdentitiesFilePath { get; set; } = "identities.json";
    public string ClientsFilePath { get; set; } = "clients.json";
}
