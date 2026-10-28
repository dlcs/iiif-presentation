using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Models.API.Manifest.Pipelines;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Repository.Converters;

public class PipelineConfigConverter : ValueConverter<IPipelineConfig, string>
{
    // camelCase matches the casing used by the API. Reading is case-insensitive so rows stored as PascalCase
    // ("Action") are still read
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    private static readonly JsonSerializer Serializer = JsonSerializer.Create(SerializerSettings);

    public PipelineConfigConverter()
        : base(
            v => JsonConvert.SerializeObject(v, SerializerSettings),
            v => Deserialize(v))
    {
    }

    // The type of config depends on its action
    private static IPipelineConfig Deserialize(string value)
    {
        var json = JObject.Parse(value);
        var action = json.GetValue(nameof(IPipelineConfig.Action), StringComparison.OrdinalIgnoreCase)?
            .Value<string>();

        return (IPipelineConfig)json.ToObject(PipelineHelper.GetConfigTypeByAction(action), Serializer)!;
    }
}
