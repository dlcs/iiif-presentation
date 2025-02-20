using System.Diagnostics.CodeAnalysis;

namespace Core.Helpers;

public static class StringX
{
    /// <summary>
    /// Check if string has content (is not null, empty or whitespace)
    /// </summary>
    /// <param name="str">String to check</param>
    /// <returns>true if string contains content; else false</returns>
    public static bool HasText([NotNullWhen(true)] this string? str) => !string.IsNullOrWhiteSpace(str);
    
    /// <summary>
    /// Appends values to a list
    /// </summary>
    /// <param name="list">The list to append to</param>
    /// <param name="values">The values to append</param>
    /// <typeparam name="T">The type for the list</typeparam>
    /// <returns>The list with values appended</returns>
    public static List<T> Append<T>(this List<T> list, params T[] values)
    {
        list.AddRange(values);
        return list;
    }

    /// <summary>
    /// Appends a value to a list if the condition is true
    /// </summary>
    /// <param name="list">The list to append to</param>
    /// <param name="condition">The condition to check against</param>
    /// <param name="values">The values to append</param>
    /// <typeparam name="T">The type for the list</typeparam>
    /// <returns>A list with all values that pass the condition</returns>
    public static List<T> AppendIf<T>(this List<T> list, bool condition, params T[] values)
    {
        return condition ? list.Append(values) : list;
    }

    /// <summary>
    /// Gets the last path element of a string
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>The last path element</returns>
    public static string GetLastPathElement(this string path)
    {
        return path.Split('/').Last();
    }
}
