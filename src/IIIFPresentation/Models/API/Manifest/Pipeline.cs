using Models.Database.General;
using Newtonsoft.Json;

namespace Models.API.Manifest;

/// <summary>
/// API representation of a pipeline job
/// </summary>
public class PipelineItem
{
    public string? Name { get; set; }
    public PipelineConfig? Config { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Status { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Error { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Warning { get; set; }
    public DateTime Created { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public DateTime? Finished { get; set; }
}

public class PipelineConfig
{
    public string? Action { get; set; }
}

public static class PipelineHelper
{
    public static class TextPipeline
    {
        public const string Name = "text";
        public static readonly string[] Actions = ["Index"];
        public const string NoTextFoundWarning = "No text resources found";
    }

    /// <summary>
    /// Remove any invalid pipelines from the manifest. This will only leave known, recognised pipelines. This makes
    /// general handling of Manifest simple as we can assume that all pipelines are known.
    /// </summary>
    /// <param name="manifest"></param>
    /// <returns>The same manifest, to allow fluent chaining.</returns>
    public static PresentationManifest RemoveInvalidPipelines(this PresentationManifest manifest)
    {
        manifest.Pipeline?.RemoveAll(pipelineItem => !IsKnownPipeline(pipelineItem));
        return manifest;
    }

    // The only recognised pipelines are "text" with action of "Index"
    private static bool IsKnownPipeline(PipelineItem pipelineItem) =>
        string.Equals(pipelineItem.Name, TextPipeline.Name, StringComparison.OrdinalIgnoreCase) &&
        TextPipeline.Actions.Contains(pipelineItem.Config?.Action, StringComparer.OrdinalIgnoreCase);

    public static bool HasPipelineJob(this PresentationManifest manifest) =>
        manifest.Pipeline?.Count > 0;

    public static PipelineItem ToPipelineItem(this PipelineJob job) => new ()
    {
        Name = job.JobType == PipelineJobType.TextService ? TextPipeline.Name : "unknown",
        Config = job.Config,
        Status = job.Status.ToString(),
        Error = job.Error,
        Warning = job.Status == PipelineJobStatus.CompletedNoOperation ? TextPipeline.NoTextFoundWarning : null,
        Created = job.Created,
        Finished = job.Finished
    };
}
