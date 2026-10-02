using IIIF.Presentation.V3;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.Manifests;

/// <summary>
/// Augments a manifest with content from a particular source (e.g. DLCS assets, text-services search).
/// Implementations take a manifest, enrich it from their source, and return the augmented manifest.
/// </summary>
public interface IManifestAugmentor
{
    /// <summary>
    /// Enrich <paramref name="manifest"/> with this source's content, returning the augmented manifest.
    /// </summary>
    Task<Manifest> Augment(Manifest manifest, DbManifest dbManifest, CancellationToken cancellationToken);
}
