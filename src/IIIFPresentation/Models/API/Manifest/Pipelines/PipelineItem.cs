using Newtonsoft.Json;

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
    public DateTime Created { get; set; }
    public DateTime? Finished { get; set; }
}
