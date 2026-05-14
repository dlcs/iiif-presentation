using API.Features.Common.Helpers;
using Core.Helpers;
using DLCS.Models;
using JsonDiffPatchDotNet;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Services.Manifests.Helpers;
using Services.Manifests.Model;
using PresUpdateResult = API.Infrastructure.Requests.ModifyEntityResult<Models.API.Manifest.PresentationManifest, Models.API.General.ModifyCollectionType>;

namespace API.Features.Manifest.Helpers;

public static class AssetDuplicateValidator
{
    public static PresUpdateResult? ValidateDuplicates(
        List<InterimCanvasPainting> paintedResourceCanvasPaintings,
        List<PaintedResource>? paintedResources)
        => ValidateDuplicateAssets(paintedResourceCanvasPaintings, paintedResources)
           // run the adjunct validator if the asset validator returned no errors
           ?? ValidateDuplicateAdjuncts(paintedResourceCanvasPaintings);

    private static PresUpdateResult? ValidateDuplicateAssets(
        List<InterimCanvasPainting> paintedResourceCanvasPaintings,
        List<PaintedResource>? paintedResources)
    {
        if (paintedResources == null) return null;

        var seenAssets = new Dictionary<string, JObject>();
        foreach (var pr in paintedResources.Where(pr => pr.Asset != null))
        {
            var asset = pr.Asset!;
            var assetId = asset.GetRequiredValue<string>(AssetProperties.Id);

            // Include space so that the same asset ID in different spaces is not treated as a conflict
            var space = asset[AssetProperties.Space]?.Value<int>() ?? SpaceHelper.DefaultSpaceForLaterPopulation;
            var dedupKey = $"{space}/{assetId}";

            if (!seenAssets.TryAdd(dedupKey, asset))
            {
                var existing = seenAssets[dedupKey];
                if (!JToken.DeepEquals(existing, asset))
                {
                    var cp = paintedResourceCanvasPaintings.FirstOrDefault(c => c.SuspectedAssetId == assetId);
                    return UpsertErrorHelper.AssetsDataDoesNotMatch<PresentationManifest>(
                        cp != null ? BuildAssetKey(cp) : assetId,
                        SerializeDiff(existing, asset));
                }
            }
        }

        return null;
    }

    private static PresUpdateResult? ValidateDuplicateAdjuncts(
        List<InterimCanvasPainting> paintedResourceCanvasPaintings)
    {
        var seenAdjuncts = new Dictionary<string, List<JObject>?>();
        foreach (var cp in paintedResourceCanvasPaintings.Where(cp => cp.SuspectedAssetId != null))
        {
            var key = BuildAssetKey(cp);
            var adjuncts = cp.AdjunctInteraction?.Adjuncts;

            if (!seenAdjuncts.TryAdd(key, adjuncts))
            {
                var existing = seenAdjuncts[key];
                if (!AdjunctsEqual(existing, adjuncts))
                    return UpsertErrorHelper.AssetAdjunctsDoNotMatch<PresentationManifest>(key,
                        SerializeDiff(new JArray(existing ?? []), new JArray(adjuncts ?? [])));
            }
        }

        return null;
    }

    // Compares two adjunct lists without regard to order.
    // Each adjunct is looked up by its id in the previous list, then deep-compared —
    // so reordering adjuncts is not treated as a conflict, but any property change is.
    // Relies on AdjunctValidator having already enforced non-empty ids upstream.
    private static bool AdjunctsEqual(List<JObject>? previous, List<JObject>? current)
    {
        // makes sure [] and null isn't allowed
        if (previous is null != current is null) return false;
        
        var left = previous ?? [];
        var right = current ?? [];
        if (left.Count != right.Count) return false;
        // index the previous adjuncts by id for O(n) lookup
        var leftById = left.ToDictionary(j => j.GetRequiredValue<string>(AssetProperties.Id));
        return right.All(j =>
            leftById.TryGetValue(j.GetRequiredValue<string>(AssetProperties.Id), out var match)
            && JToken.DeepEquals(j, match));
    }

    private static string SerializeDiff(JToken left, JToken right)
        => JsonConvert.SerializeObject(new JsonDiffPatch().Diff(left, right));

    // Omits the space sentinel from user-facing keys — space is unknown until DLCS interaction
    private static string BuildAssetKey(InterimCanvasPainting cp) =>
        cp.SuspectedSpace == null || cp.SuspectedSpace == SpaceHelper.DefaultSpaceForLaterPopulation
            ? $"{cp.CustomerId}/{cp.SuspectedAssetId}"
            : $"{cp.CustomerId}/{cp.SuspectedSpace}/{cp.SuspectedAssetId}";
}
