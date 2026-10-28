namespace Models.API.Manifest.Pipelines;

/// <summary>
/// Config for the "text" pipeline
/// </summary>
public class TextPipelineConfig : IPipelineConfig
{
    public string? Action { get; set; }

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
