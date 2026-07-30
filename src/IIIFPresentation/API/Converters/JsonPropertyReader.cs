using Newtonsoft.Json.Linq;

namespace API.Converters;

/// <summary>
/// Reads a single top-level string property out of a raw JSON request body without deserializing it into a full
/// model. Used where a routing/dispatch decision needs to be made from the body before it's known which model to
/// deserialize into - e.g. hierarchical POST/PUT inspecting the top-level <c>"type"</c> to route between Collection
/// and Manifest handling.
/// </summary>
public static class JsonPropertyReader
{
    /// <summary>
    /// Reads a top-level string property from a request body. Returns null if the body isn't valid JSON, has no
    /// top-level property with this name, or the property isn't a string.
    /// </summary>
    public static string? ReadTopLevelString(string rawJson, string propertyName, ILogger? logger = null)
    {
        try
        {
            var json = JObject.Parse(rawJson);
            return json[propertyName]?.Type == JTokenType.String ? json[propertyName]!.Value<string>() : null;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Could not read top-level property '{PropertyName}' from request body",
                propertyName);
            return null;
        }
    }
}
