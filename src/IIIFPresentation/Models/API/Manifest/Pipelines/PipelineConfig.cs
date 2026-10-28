using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Models.API.Manifest.Pipelines;

/// <summary>
/// Config for a pipeline that has no specific config type, or whose config couldn't be read as that type. Settings
/// are kept as provided.
/// </summary>
public class PipelineConfig : IPipelineConfig
{
    public string? Action { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken>? Settings { get; set; }
}
