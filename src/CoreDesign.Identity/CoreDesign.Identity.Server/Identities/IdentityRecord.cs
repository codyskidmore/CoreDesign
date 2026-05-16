namespace CoreDesign.Identity.Server;

public class IdentityRecord
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string GivenName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = [];
    public Dictionary<string, string> CustomClaims { get; set; } = [];
}
