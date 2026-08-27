using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using IIIF.Presentation.V3;

namespace API.Converters;

/// <summary>
/// Reads a single string property out of a raw JSON request body without deserializing it into a full model. Used
/// where a routing/dispatch decision needs to be made from the body before it's known which model to deserialize
/// into - e.g. hierarchical POST/PUT inspecting the top-level <c>"type"</c> to route between Collection and
/// Manifest handling.
/// </summary>
/// <remarks>
/// Uses <see cref="Utf8JsonReader"/> to scan tokens directly rather than parsing the body into a full JSON DOM -
/// for a large manifest body (many canvases/items) this is significantly cheaper, since it stops as soon as the
/// target property is found instead of allocating a node for every property/array element in the document.
/// </remarks>
public static class JsonPropertyReader
{
    /// <summary>
    /// Reads a string property at the given depth from a request body. Returns null if the body isn't valid JSON,
    /// has no property with this name at this depth, or the property isn't a string.
    /// </summary>
    /// <param name="rawJson">JSON to read through</param>
    /// <param name="propertyName">Name of the property to find</param>
    /// <param name="level">Depth of the property - 1 is the top level</param>
    /// <param name="logger">Logger, used to record why a read failed</param>
    public static string? ReadJsonProperty(string rawJson, string propertyName, int level = 1,
        ILogger? logger = null)
    {
        try
        {
            var utf8 = Encoding.UTF8.GetBytes(rawJson);
            var reader = new Utf8JsonReader(utf8, isFinalBlock: true, state: default);

            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != level) continue;
                if (!reader.ValueTextEquals(propertyName)) continue;

                if (!reader.Read()) return null;
                return reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            }

            return null;
        }
        catch (JsonException ex)
        {
            logger?.LogDebug(ex, "Could not read property '{PropertyName}' at level {Level} from request body",
                propertyName, level);
            return null;
        }
    }

    /// <summary>
    /// Try and get the "type" property from raw string.
    /// </summary>
    /// <param name="rawJson">JSON to read</param>
    /// <param name="logger">Current logger instance</param>
    /// <param name="type">
    /// Type value, if found. Will only be non-null if a value for type was "Manifest" or "Collection"
    /// </param>
    /// <returns>true if a valid value (ie "Manifest" or "Collection") found, else false</returns>
    public static bool TryGetValidType(string rawJson, ILogger? logger, [NotNullWhen(true)] out string? type)
    {
        const string typeProperty = "type";
        
        type = ReadJsonProperty(rawJson, typeProperty, 1, logger);
        if (type == null) return false;
        if (type is nameof(Manifest) or nameof(Collection)) return true;
        
        type = null;
        return false;
    }
}
