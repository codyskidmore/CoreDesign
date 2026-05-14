using System.Reflection;
using Microsoft.AspNetCore.Hosting;

namespace CoreDesign.Identity.Server;

/// <summary>
/// Loads HTML templates from an optional host-project override folder, falling back
/// to the embedded defaults shipped with the library.
///
/// To override a template, place a file with the same name inside an
/// "identity-templates" folder at the host project's content root and set
/// CopyToOutputDirectory to PreserveNewest.
/// </summary>
internal static class TemplateLoader
{
    /// <summary>
    /// Folder name, relative to the host project's content root, where template
    /// overrides are discovered. Placing a file here with the same name as an
    /// embedded template replaces the built-in default.
    /// </summary>
    internal const string OverrideFolder = "identity-templates";

    /// <summary>
    /// Returns the template content for <paramref name="fileName"/>, checking the
    /// host content root override folder before falling back to the embedded default.
    /// </summary>
    internal static string Load(string fileName, IWebHostEnvironment env)
    {
        var overridePath = Path.Combine(env.ContentRootPath, OverrideFolder, fileName);
        if (File.Exists(overridePath))
            return File.ReadAllText(overridePath);

        return LoadEmbedded(fileName);
    }

    /// <summary>
    /// Replaces every <c>{{key}}</c> placeholder in <paramref name="template"/> with
    /// the corresponding value from <paramref name="values"/>. Keys not present in the
    /// dictionary are left untouched.
    /// </summary>
    internal static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        foreach (var (key, value) in values)
            template = template.Replace($"{{{{{key}}}}}", value, StringComparison.Ordinal);
        return template;
    }

    private static string LoadEmbedded(string fileName)
    {
        var assembly = typeof(TemplateLoader).Assembly;
        var resourceName = $"CoreDesign.Identity.Server.Templates.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded template '{resourceName}' was not found. " +
                $"Verify the file is marked as EmbeddedResource in the project file.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
