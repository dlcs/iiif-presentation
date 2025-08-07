using System.Collections.Frozen;

namespace Models.API.General;

public static class SpecConstants
{
    public static readonly FrozenSet<string> ProhibitedSlugs =
        new[]
        {
            "collections",
            "manifests",
            "paintedResources",
            "canvases",
            "annotations",
            "adjuncts",
            "pipelines",
            "queue",
            "assets",
            "configuration",
            "publish"
        }.ToFrozenSet(StringComparer.InvariantCultureIgnoreCase);
    
    public const string CollectionsSlug = "collections";
    public const string ManifestsSlug = "manifests";
    public const string CanvasesSlug = "canvases";
}
