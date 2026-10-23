using Models.Database.General;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Models.API.Manifest;

/// <summary>
/// API representation of a pipeline job
/// </summary>
public class PipelineItem
{
    public string? Name { get; set; }
    public PipelineConfig? Config { get; set; }
    public string? Status { get; set; }
    public string? Error { get; set; }
    public string? Warning { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Finished { get; set; }
}

/// <summary>
/// Config for a pipeline. <see cref="Action"/> is common to all pipelines, anything else is specific to the type of
/// pipeline and is read via a typed view, e.g. <see cref="PipelineHelper.AsTextConfig"/>.
/// </summary>
public class PipelineConfig
{
    public string? Action { get; set; }

    /// <summary>
    /// Pipeline-specific settings, kept as provided so they round-trip through storage and API responses
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JToken>? Settings { get; set; }
}

/// <summary>
/// Settings specific to the "text" pipeline, read from <see cref="PipelineConfig.Settings"/>
/// </summary>
public class TextPipelineConfig
{
    /// <summary>
    /// The only settings recognised for the "text" pipeline
    /// </summary>
    public static readonly string[] SettingNames = ["output", "pdfCoverPage"];

    /// <summary>
    /// Outputs to generate and include on the Manifest - "All" or text-services service flag names. Null means
    /// "All".
    /// </summary>
    public List<string>? Output { get; set; }

    /// <summary>
    /// URI of a PDF to prepend to the generated PDF as a cover page. When set, a PDF is always generated.
    /// </summary>
    public string? PdfCoverPage { get; set; }
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
        pipelineItem.IsTextPipeline() &&
        TextPipeline.Actions.Contains(pipelineItem.Config?.Action, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this is a "text" pipeline (case-insensitive). Says nothing about whether its config is valid.
    /// </summary>
    public static bool IsTextPipeline(this PipelineItem pipelineItem) =>
        string.Equals(pipelineItem.Name, TextPipeline.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Read the "text" pipeline settings from this config. Throws a <see cref="JsonException"/> if a setting has the
    /// wrong type - use <see cref="TryGetTextConfig"/> where that hasn't already been validated.
    /// </summary>
    public static TextPipelineConfig AsTextConfig(this PipelineConfig? config) =>
        config?.Settings == null
            ? new TextPipelineConfig()
            : JObject.FromObject(config.Settings).ToObject<TextPipelineConfig>() ?? new TextPipelineConfig();

    /// <summary>
    /// Try to read the "text" pipeline settings from this config, returning false if a setting has the wrong type.
    /// </summary>
    public static bool TryGetTextConfig(this PipelineConfig? config, out TextPipelineConfig textConfig)
    {
        try
        {
            textConfig = config.AsTextConfig();
            return true;
        }
        catch (JsonException)
        {
            textConfig = new TextPipelineConfig();
            return false;
        }
    }

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
