using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoreDesign.Shared.ExtensionMethods;

public static class StringExtensionMethods
{
    /// <summary>
    ///     Loads an object of type "T" from a JSON file.
    /// </summary>
    /// <typeparam name="T">The object type to create.</typeparam>
    /// <param name="pathAndFileName">The path and filename of the JSON file.</param>
    /// <returns>An object of type "T"</returns>
    public static T? LoadObjectFromJsonFile<T>(this string pathAndFileName)
    {
        var json = File.ReadAllText(pathAndFileName);

        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            // WriteIndented = false, Holding in case we need compact style 
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        });
    }

    public static T? To<T>(this string value)
    {
        var type = typeof(T);
        var converter = TypeDescriptor.GetConverter(type);
        if (!converter.CanConvertFrom(typeof(string)))
            throw new ArgumentException($"Cannot convert from string to type {type}.");

        try
        {
            return (T)converter.ConvertFromInvariantString(value)!;
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Error while converting from string to type '{type}' for value '{value}'.",
                ex);
        }
    }

    public static string PadRightWithZeros(this string input, int totalWidth)
    {
        if (string.IsNullOrEmpty(input)) return new string('0', totalWidth);

        return input.PadRight(totalWidth, '0');
    }
}