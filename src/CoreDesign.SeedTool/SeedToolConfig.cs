using System.Text.Json.Serialization;

namespace CoreDesign.SeedTool;

internal sealed class SeedToolConfig
{
    [JsonPropertyName("assemblyPath")]
    public string AssemblyPath { get; init; } = "";

    [JsonPropertyName("outputDirectory")]
    public string OutputDirectory { get; init; } = "SeedData";

    [JsonPropertyName("entities")]
    public IReadOnlyList<string> Entities { get; init; } = [];

    [JsonPropertyName("connectionString")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConnectionString { get; init; }

    [JsonPropertyName("userSecrets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UserSecretsSource? UserSecrets { get; init; }

    [JsonPropertyName("appConfig")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AppConfigSource? AppConfig { get; init; }
}

internal sealed class UserSecretsSource
{
    [JsonPropertyName("project")]
    public string Project { get; init; } = "";

    [JsonPropertyName("passwordKey")]
    public string PasswordKey { get; init; } = "";
}

internal sealed class AppConfigSource
{
    [JsonPropertyName("project")]
    public string Project { get; init; } = "";

    [JsonPropertyName("connectionStringKey")]
    public string ConnectionStringKey { get; init; } = "";
}
