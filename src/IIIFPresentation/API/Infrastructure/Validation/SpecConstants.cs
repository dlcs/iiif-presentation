﻿using System.Collections.Frozen;

namespace API.Infrastructure.Validation;

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
}
