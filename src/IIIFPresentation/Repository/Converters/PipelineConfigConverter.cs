using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Models.API.Manifest;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Repository.Converters;

public class PipelineConfigConverter : ValueConverter<PipelineConfig, string>
{
    // camelCase matches the casing of the pipeline-specific settings, which are stored as provided. Reading is
    // case-insensitive so rows stored as PascalCase ("Action") are still read
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    public PipelineConfigConverter()
        : base(
            v => JsonConvert.SerializeObject(v, SerializerSettings),
            v => JsonConvert.DeserializeObject<PipelineConfig>(v, SerializerSettings)!)
    {
    }
}
