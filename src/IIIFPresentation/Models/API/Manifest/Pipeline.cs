using Models.Database.General;
using Newtonsoft.Json;

namespace Models.API.Manifest;

public class PipelineItem
{
    public string? Name { get; set; }
    public PipelineConfig? Config { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Status { get; set; }
}

public class PipelineConfig
{
    public string? Action { get; set; }
}

public static class PipelineX
{
    public const string TextPipelineName = "text";

    public static bool HasPipelineJob(this PresentationManifest manifest) =>
        manifest.Pipeline?.Count > 0;

    public static PipelineItem ToPipelineItem(this PipelineJob job) => new ()
    {
        Name = job.JobType.ToString(),
        Config = job.Config,
        Status = job.Status.ToString()
    };
}
