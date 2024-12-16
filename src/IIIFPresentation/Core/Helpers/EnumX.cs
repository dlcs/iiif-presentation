using System.ComponentModel;
using System.Reflection;

namespace Core.Helpers;

public static class EnumX
{
    /// <summary>
    /// Find enum value for specified string.
    /// Matches in order of precedence: Exact value -> DescriptionAttribute. 
    /// </summary>
    /// <param name="description">String to find enum for.</param>
    /// <param name="defaultIfNotFound">
    /// Whether to return default(T) if value not found.
    /// If false throws exception of not found.
    /// </param>
    /// <typeparam name="T">Type of enum.</typeparam>
    /// <returns>Matching enum value, if found.</returns>
    public static T? GetEnumFromString<T>(this string description, bool defaultIfNotFound = true)
        where T : System.Enum
    {
        description.ThrowIfNullOrWhiteSpace(nameof(description));
        var memberInfos = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in memberInfos)
        {
            if (field.Name == description)
            {
                return (T?)field.GetRawConstantValue();
            }

            if (field.GetCustomAttribute<DescriptionAttribute>()?.Description == description)
            {
                return (T?)field.GetRawConstantValue();
            }
        }

        return defaultIfNotFound
            ? default(T)
            : throw new ArgumentException($"Matching enum for '{description}' not found.", nameof(description));
    }
}
