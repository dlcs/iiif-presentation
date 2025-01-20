using API.Helpers;
using Core.Helpers;
using Core.IIIF;
using IIIF;
using IIIF.Presentation;
using Models.API.Manifest;
using Models.Database.Collections;
using Models.Database.General;
using Newtonsoft.Json.Linq;
using CanvasPainting = Models.Database.CanvasPainting;

namespace API.Converters;

public static class ManifestConverter
{
    /// <summary>
    /// Update <see cref="PresentationManifest"/> with values from DB record.
    /// </summary>
    /// <param name="iiifManifest">Presentation Manifest to update</param>
    /// <param name="dbManifest">Database Manifest</param>
    /// <param name="pathGenerator">used to generate paths</param>
    /// <param name="assets"></param>
    /// <param name="hierarchyFactory">
    ///     Optional factory to specify <see cref="Hierarchy"/> to use to get Parent and Slug. Defaults to using .Single()
    /// </param>
    public static PresentationManifest SetGeneratedFields(this PresentationManifest iiifManifest,
        Manifest dbManifest, IPathGenerator pathGenerator, Dictionary<string, JObject>? assets = null,
        Func<Manifest, Hierarchy>? hierarchyFactory = null)
    {
        hierarchyFactory ??= manifest => manifest.Hierarchy.ThrowIfNull(nameof(manifest.Hierarchy)).Single();
        
        var hierarchy = hierarchyFactory(dbManifest);
        
        iiifManifest.Id = pathGenerator.GenerateFlatManifestId(dbManifest);
        iiifManifest.FlatId = dbManifest.Id;
        iiifManifest.PublicId = pathGenerator.GenerateHierarchicalId(hierarchy);
        iiifManifest.Created = dbManifest.Created.Floor(DateTimeX.Precision.Second);
        iiifManifest.Modified = dbManifest.Modified.Floor(DateTimeX.Precision.Second);
        iiifManifest.CreatedBy = dbManifest.CreatedBy;
        iiifManifest.ModifiedBy = dbManifest.ModifiedBy;
        iiifManifest.Parent = pathGenerator.GenerateFlatParentId(hierarchy);
        iiifManifest.Slug = hierarchy.Slug;
        iiifManifest.PaintedResources = dbManifest.GetPaintedResources(pathGenerator, assets);
        iiifManifest.Space = pathGenerator.GenerateSpaceUri(dbManifest)?.ToString();
        iiifManifest.EnsurePresentation3Context();
        iiifManifest.EnsureContext(PresentationJsonLdContext.Context);
        
        return iiifManifest;
    }

    private static List<PaintedResource>? GetPaintedResources(this Manifest dbManifest, IPathGenerator pathGenerator,
        Dictionary<string, JObject>? assets)
    {
        if (dbManifest.CanvasPaintings.IsNullOrEmpty()) return null;

        return dbManifest.CanvasPaintings.Select(cp => new PaintedResource
        {
            CanvasPainting = new Models.API.Manifest.CanvasPainting
            {
                CanvasId = pathGenerator.GenerateCanvasId(cp),
                Thumbnail = cp.Thumbnail?.ToString(),
                StaticHeight = cp.StaticHeight,
                CanvasOrder = cp.CanvasOrder,
                ChoiceOrder = cp.ChoiceOrder,
                StaticWidth = cp.StaticWidth,
                Target = cp.Target,
                Label = cp.Label,
                CanvasOriginalId = cp.CanvasOriginalId?.ToString(),
                CanvasLabel = cp.CanvasLabel,
            },
            Asset = GetAsset(cp, pathGenerator, assets)
        }).ToList();
    }

    private static JObject? GetAsset(CanvasPainting cp, IPathGenerator pathGenerator,
        Dictionary<string, JObject>? assets)
    {
        if (cp.AssetId == null)
            return null;

        var fullAssetId = pathGenerator.GenerateAssetUri(cp)?.ToString();
        if (fullAssetId == null)
            return null;

        return assets is not null && assets.TryGetValue(fullAssetId, out var asset)
            ? asset
            : new()
            {
                ["@id"] = fullAssetId
            };
    }
}
