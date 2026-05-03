namespace CoreDesign.Identity.Server;

public class IdentityOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string KeyId { get; set; } = "coredesign-dev-signing-key";
    public int TokenLifetimeHours { get; set; } = 8;
}
