namespace Models.API.Manifest.Pipelines;

/// <summary>
/// Config for a pipeline. <see cref="Action"/> is common to all pipelines, anything else is specific to the type of
/// pipeline.
/// </summary>
public interface IPipelineConfig
{
    string? Action { get; set; }
}
