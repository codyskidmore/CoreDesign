using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoreDesign.Shared.ExtensionMethods;

/// <summary>
///     Converts any object to JSON.
///     Not used in the project after discovering Serilog can convert objects to JSON without it. Leaving the code here as
///     a placeholder for future use.
/// </summary>
public static class ObjectExtensionMethods
{
    public static string ToJson<TObject>(this TObject obj)
    {
        return JsonSerializer.Serialize(obj,
            new JsonSerializerOptions
            {
                // WriteIndented = false, Holding in case we need compact style 
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            });
    }

    public static void SaveToJsonFile<TObject>(this TObject obj, string filePath)
    {
        var json = ToJson(obj);
        File.WriteAllText(filePath, json);
    }

    private static readonly JsonSerializerOptions DefaultCloneOptions = new JsonSerializerOptions
    {
        IncludeFields = true,
        ReferenceHandler = ReferenceHandler.Preserve,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Deep clones an object using System.Text.Json serialization.
    /// Returns null if source is null.
    /// Note: types must be serializable by System.Text.Json (constructors, [JsonConstructor], etc.).
    /// </summary>
    public static T? DeepClone<T>(this T? source, JsonSerializerOptions? options = null)
    {
        if (source is null) return default;
        var opts = options ?? DefaultCloneOptions;
        var json = JsonSerializer.Serialize(source, opts);
        return JsonSerializer.Deserialize<T>(json, opts);
    }
}