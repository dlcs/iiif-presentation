using System.Text;
using System.Text.Json;

namespace API.Converters;

public static class FastJsonPropertyRead
{
    /// <summary>
    /// Uses <see cref="Utf8JsonReader"/> to find a value of a specified property, at the specified level (1 - top level)
    /// </summary>
    /// <param name="json">JSON to read through</param>
    /// <param name="targetPropertyName">name of the property to find</param>
    /// <param name="level">Depth of the property - 1 is the top level</param>
    /// <returns>string representation of the property value or null if property is null or not present</returns>
    /// <remarks>
    /// This can be reused with minor refactoring to allow for e.g. byte input, stream input etc., but as in the
    /// current use case this is not required, I'm not overcomplicating this method. Same for other types (number, date...)
    /// </remarks>
    public static string? FindAtLevel(string json, string targetPropertyName, int level = 1)
    {
        ReadOnlySpan<byte> utf8 = Encoding.UTF8.GetBytes(json);

        var reader = new Utf8JsonReader(utf8, isFinalBlock: true, state: default);

        // to avoid allocating strings for each comparison
        ReadOnlySpan<byte> targetUtf8 = Encoding.UTF8.GetBytes(targetPropertyName);

        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName ||
                reader.CurrentDepth != level)
            {
                continue;
            }
            
            if (reader.ValueTextEquals(targetUtf8))
            {
                if (!reader.Read())
                    throw new JsonException("Unexpected end of JSON after property name.");

                return reader.GetString(); // return string representation of the value
            }
        }

        return null; // not found
    } 
}
