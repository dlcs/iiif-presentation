using Models.Database.General;

namespace Models.API.Manifest.Pipelines;

public static class PipelineHelper
{
    public static class TextPipeline
    {
        public const string Name = "text";
        public const string IndexAction = "Index";
        public static readonly string[] Actions = [IndexAction];
        public const string NoTextFoundWarning = "No text resources found";
    }

    /// <summary>
    /// A recognised pipeline, the actions it supports and the type of its config
    /// </summary>
    private record PipelineDefinition(string Name, string[] Actions, Type ConfigType);

    /// <summary>
    /// Every recognised pipeline. Adding a pipeline only needs an entry here.
    /// </summary>
    private static readonly PipelineDefinition[] Pipelines =
    [
        new(TextPipeline.Name, TextPipeline.Actions, typeof(TextPipelineConfig))
    ];

    private static readonly Dictionary<string, PipelineDefinition> PipelinesByName =
        Pipelines.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    // Stored config only has its action, so an action used by more than one pipeline would be ambiguous. ToDictionary
    // throws on a duplicate action rather than picking one.
    private static readonly Dictionary<string, Type> ConfigTypesByAction = Pipelines
        .SelectMany(p => p.Actions.Select(action => (Action: action, p.ConfigType)))
        .ToDictionary(a => a.Action, a => a.ConfigType, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The type of config for the pipeline with this name, or <see cref="PipelineConfig"/> if the pipeline isn't
    /// recognised
    /// </summary>
    public static Type GetConfigTypeByName(string? name) =>
        name != null && PipelinesByName.TryGetValue(name, out var pipeline)
            ? pipeline.ConfigType
            : typeof(PipelineConfig);

    /// <summary>
    /// The type of config for a pipeline with this action, or <see cref="PipelineConfig"/> if the action has no
    /// specific config type
    /// </summary>
    public static Type GetConfigTypeByAction(string? action) =>
        action != null && ConfigTypesByAction.TryGetValue(action, out var configType)
            ? configType
            : typeof(PipelineConfig);

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

    // A pipeline is recognised if it's in Pipelines and its action is one that pipeline supports
    private static bool IsKnownPipeline(PipelineItem pipelineItem) =>
        pipelineItem.Name != null &&
        PipelinesByName.TryGetValue(pipelineItem.Name, out var pipeline) &&
        pipeline.Actions.Contains(pipelineItem.Config?.Action, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this is a "text" pipeline (case-insensitive). Says nothing about whether its config is valid.
    /// </summary>
    public static bool IsTextPipeline(this PipelineItem pipelineItem) =>
        string.Equals(pipelineItem.Name, TextPipeline.Name, StringComparison.OrdinalIgnoreCase);

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
