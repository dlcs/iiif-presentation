using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Models.API.Manifest.Pipelines;

/// <summary>
/// API representation of a pipeline job
/// </summary>
[JsonConverter(typeof(PipelineItemConverter))]
public class PipelineItem
{
    public string? Name { get; set; }
    public IPipelineConfig? Config { get; set; }
    public string? Status { get; set; }
    public string? Error { get; set; }
    public string? Warning { get; set; }

    /// <summary>
    /// Details of what a finished job did. The shape depends on the type of pipeline.
    /// </summary>
    public JObject? Result { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Finished { get; set; }
}
