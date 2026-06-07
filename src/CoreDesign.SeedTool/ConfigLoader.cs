using System.Text.Json;

namespace CoreDesign.SeedTool;

internal static class ConfigLoader
{
    internal const string ConfigFileName = "coredesign.seedtool.json";

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    internal static string? FindConfigPath(string? explicitPath = null)
    {
        if (explicitPath is not null)
            return File.Exists(explicitPath) ? Path.GetFullPath(explicitPath) : null;

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ConfigFileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    internal static SeedToolConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SeedToolConfig>(json, ReadOptions)
               ?? throw new InvalidOperationException($"Could not deserialize {ConfigFileName}.");
    }
}
