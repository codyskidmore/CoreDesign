namespace CoreDesign.Shared.Infrastructure;

/// <summary>Options for <c>AddAppSettings</c>.</summary>
public sealed class AppSettingsOptions
{
    /// <summary>
    /// Configuration key that chooses the settings file when <see cref="SettingsName"/> is not set,
    /// for example the environment variable <c>AppSettings__Name=LOCAL</c> in launchSettings.json or
    /// an App Service setting.
    /// </summary>
    public const string NameKey = "AppSettings:Name";

    /// <summary>
    /// Name used for the environment file, <c>appsettings.{SettingsName}.json</c>. Null (the default)
    /// falls back to the <see cref="NameKey"/> configuration value, then to the host's environment name.
    /// Use it to name settings files after deployment environments (LOCAL, DEV, UAT, PROD) while the
    /// local environment stays <c>Development</c>.
    /// </summary>
    public string? SettingsName { get; set; }
}
