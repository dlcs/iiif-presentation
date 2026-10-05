using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Models.API.Manifest;
using Newtonsoft.Json;

namespace Repository.Converters;

public class PipelineConfigConverter : ValueConverter<PipelineConfig, string>
{
    public PipelineConfigConverter()
        : base(
            v => JsonConvert.SerializeObject(v),
            v => JsonConvert.DeserializeObject<PipelineConfig>(v)!)
    {
    }
}