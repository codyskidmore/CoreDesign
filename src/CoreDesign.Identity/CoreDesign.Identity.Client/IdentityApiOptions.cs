namespace CoreDesign.Identity.Client;

public class IdentityApiOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "IdentityApi";

    /// <summary>Configuration key for <see cref="EnableLocalTokenInjection"/>.</summary>
    public const string EnableLocalTokenInjectionKey = SectionName + ":" + nameof(EnableLocalTokenInjection);

    public string BaseUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Whether <c>UseLocalBearerTokenInjection</c> signs tokenless localhost requests in as the
    /// configured <see cref="Username"/>. Null (the default) means on in the Development environment
    /// and off everywhere else. Set it explicitly so the behaviour does not depend on the environment
    /// name, for example <c>true</c> in local settings and <c>false</c> in every deployed environment.
    /// </summary>
    public bool? EnableLocalTokenInjection { get; set; }
}
