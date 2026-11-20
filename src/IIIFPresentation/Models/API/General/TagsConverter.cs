using Newtonsoft.Json;

namespace Models.API.General;

/// <summary>
/// Reads "tags" as either an array of strings or a single comma-separated string, e.g. <c>"one, two"</c> is read as
/// <c>["one", "two"]</c>. Entries are trimmed and empty entries are dropped. Written as a standard array.
/// </summary>
public class TagsConverter : JsonConverter<List<string>?>
{
    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, List<string>? value, JsonSerializer serializer) =>
        throw new NotSupportedException();

    public override List<string>? ReadJson(JsonReader reader, Type objectType, List<string>? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        IEnumerable<string?>? tags = reader.TokenType switch
        {
            JsonToken.Null => null,
            JsonToken.String => ((string)reader.Value!).Split(','),
            JsonToken.StartArray => serializer.Deserialize<List<string?>>(reader),
            _ => throw new JsonSerializationException(
                $"Unexpected token {reader.TokenType} reading tags; expected a string or an array of strings"),
        };

        return tags?
            .Select(t => t?.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t!)
            .ToList();
    }
}
