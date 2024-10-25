using Models.API.Manifest;
using Models.Database.Collections;

namespace API.Tests.Integration.Infrastructure;

/// <summary>
/// Helpers for creating Presentation IIIF models from entities for testing
/// </summary>
public static class IIIFModelGenerators
{
    public static PresentationManifest ToPresentationManifest(this Manifest manifest, string? slug = null,
        string? parent = null)
        => new()
        {
            Slug = slug ?? manifest.Hierarchy.Single().Slug,
            Parent = parent ?? manifest.Hierarchy.Single().Parent,
        };
}