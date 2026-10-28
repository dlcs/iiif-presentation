using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Models.API.Manifest.Pipelines;

/// <summary>
/// Reads a <see cref="PipelineItem"/>, choosing the type of <see cref="PipelineItem.Config"/> from the pipeline name
/// </summary>
public class PipelineItemConverter : JsonConverter<PipelineItem>
{
    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, PipelineItem? value, JsonSerializer serializer) =>
        throw new NotSupportedException();

    public override PipelineItem? ReadJson(JsonReader reader, Type objectType, PipelineItem? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;

        var json = JObject.Load(reader);

        // "config" can only be read once the name is known, so it's removed before populating everything else
        var config = json.GetValue("config", StringComparison.OrdinalIgnoreCase);
        config?.Parent?.Remove();

        var pipelineItem = existingValue ?? new PipelineItem();
        serializer.Populate(json.CreateReader(), pipelineItem);

        if (config is { Type: not JTokenType.Null })
        {
            pipelineItem.Config = ReadConfig(config, PipelineHelper.GetConfigTypeByName(pipelineItem.Name), serializer);
        }

        return pipelineItem;
    }

    // A config with a wrong-typed setting is read as a generic config, rather than failing to read the whole request
    private static IPipelineConfig? ReadConfig(JToken config, Type configType, JsonSerializer serializer)
    {
        try
        {
            return (IPipelineConfig?)config.ToObject(configType, serializer);
        }
        catch (JsonException) when (configType != typeof(PipelineConfig))
        {
            return config.ToObject<PipelineConfig>(serializer);
        }
    }
}
