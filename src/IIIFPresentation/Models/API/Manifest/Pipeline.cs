namespace Models.API.Manifest;

public class PipelineItem
{
    public string? Name { get; set; }
    public PipelineConfig? Config { get; set; }
}

public class PipelineConfig
{
    public string? Action { get; set; }
}

public static class PipelineX
{
    public static bool HasTextIndexPipeline(this PresentationManifest manifest) =>
        manifest.Pipeline?.Any(p =>
            string.Equals(p.Name, "text", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.Config?.Action, "Index", StringComparison.OrdinalIgnoreCase)) == true;
}