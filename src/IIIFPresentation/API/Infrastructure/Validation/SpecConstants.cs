using System.Collections.Frozen;

namespace API.Infrastructure.Validation;

public static class SpecConstants
{
    public static readonly FrozenSet<string> ProhibitedSlugs =
        new[]
        {
            "collections",
            "manifests",
            "canvases",
            "root",
            "annotations",
            "adjuncts",
            "pipelines",
            "configuration"
        }.ToFrozenSet(StringComparer.InvariantCultureIgnoreCase);
}