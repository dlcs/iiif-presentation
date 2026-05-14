using Core.Helpers;
using IIIF.Presentation.V3;
using Models.API.Manifest;
using Models.DLCS;
using Services.Manifests.Model;

namespace Services.Manifests.Helpers;

/// <summary>
/// Generates adjunct interactions for resource level adjuncts
/// </summary>
public static class ResourceAdjunctInteractions
{
    public const int StubAssetSpace = 0;

    /// <summary>
    /// The <see cref="AssetId"/> for the manifest-level stub asset in space 0, given the resources's internal id.
    /// </summary>
    public static AssetId GetResourceStubAssetId(ResourceBase resource, int customerId, string resourceId) =>
        new(customerId, StubAssetSpace, $"{resource.Type}_{resourceId}");

    /// <summary>
    /// Builds an <see cref="AdjunctInteraction"/> for the given stub asset and adjunct list.
    /// Returns null if <see cref="PresentationManifest.Adjuncts"/> is null (treat as "no change").
    /// Note: stamps <see cref="AssetProperties.Asset"/> onto each adjunct JObject in-place.
    /// </summary>
    public static AdjunctInteraction? GetAdjunctInteraction(PresentationManifest presentationManifest, int customerId)
    {
        // We only check for null here, as an empty list signals we want to delete all adjuncts
        if (presentationManifest.Adjuncts == null) return null;

        var stubAssetId = GetResourceStubAssetId(presentationManifest, customerId, presentationManifest.Id!);

        var hydratedAdjuncts = presentationManifest.Adjuncts
            .Select(a =>
            {
                a[AssetProperties.Asset] = stubAssetId.ToString();
                return a;
            })
            .ToList();

        return new AdjunctInteraction { AssetId = stubAssetId, Adjuncts = hydratedAdjuncts };
    }
}
