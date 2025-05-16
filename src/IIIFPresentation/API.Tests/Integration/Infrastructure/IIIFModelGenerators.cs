using IIIF.Presentation.V3.Strings;
using Models.API.Manifest;
using Models.Database.Collections;

namespace API.Tests.Integration.Infrastructure;

/// <summary>
/// Helpers for creating Presentation IIIF models from entities for testing
/// </summary>
public static class IIIFModelGenerators
{
    public static PresentationManifest ToPresentationManifest(this Manifest manifest, string? slug = null,
        string? parent = null, LanguageMap? label = null)
        => new()
        {
            Slug = slug ?? manifest.Hierarchy.Single().Slug,
            Parent = Uri.IsWellFormedUriString(parent, UriKind.Absolute) ? 
                parent :  
                $"http://localhost/{manifest.CustomerId}/collections/{parent ?? manifest.Hierarchy.Single().Parent}",
            Label = label
        };
}
