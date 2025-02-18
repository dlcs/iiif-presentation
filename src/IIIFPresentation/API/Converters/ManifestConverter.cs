using API.Helpers;
using Core.Helpers;
using Core.IIIF;
using IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Models.API.Manifest;
using Models.Database.Collections;
using Models.Database.General;
using Models.Infrastructure;
using Newtonsoft.Json.Linq;
using Repository;
using Repository.Paths;
using CanvasPainting = Models.Database.CanvasPainting;
using Manifest = Models.Database.Collections.Manifest;

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

        // Note ??= - this is only if we don't yet have Items set by background process
        iiifManifest.Items ??= GenerateProvisionalItems(iiifManifest.PaintedResources);

        if (dbManifest.IsIngesting())
        {
            iiifManifest.Ingesting = GenerateIngesting(assets);
        }
        
        iiifManifest.EnsurePresentation3Context();
        iiifManifest.EnsureContext(PresentationJsonLdContext.Context);
        
        return iiifManifest;
    }

    private static List<Canvas>? GenerateProvisionalItems(List<PaintedResource>? paintedResources)
    {
        if (paintedResources is not {Count: > 0})
            return null;

        return paintedResources
            .GroupBy(pr => pr.CanvasPainting.CanvasOrder)
            .Select(GenerateProvisionalCanvas)
            .ToList();

        Canvas GenerateProvisionalCanvas(IGrouping<int?, PaintedResource> canvasPaintings)
        {
            var canvasId = canvasPaintings.First().CanvasPainting.CanvasId;
            var c = new Canvas
            {
                Id = canvasId,
                Items =
                [
                    new()
                    {
                        Id = $"{canvasId}/annopages/{canvasPaintings.Key}",
                        Items =
                        [
                            new PaintingAnnotation
                            {
                                Id = $"{canvasId}/annotations/{canvasPaintings.Key}",
                                Behavior = [Behavior.Processing],
                                Target = new Canvas {Id = canvasId},
                                Body = GetBody(canvasPaintings)
                            }
                        ]
                    }
                ]
            };

            return c;
        }

        IPaintable? GetBody(IGrouping<int?, PaintedResource> paintings)
        {
            if (paintings.Count() > 1)
                return new PaintingChoice
                {
                    Items = paintings.OrderBy(p => p.CanvasPainting.ChoiceOrder ?? -1).Select(GetImage).ToList()
                };

            return GetImage(paintings.Single());
        }

        IPaintable GetImage(PaintedResource paintedResource)
        {
            return new Image();
        }
    }

    private static List<PaintedResource>? GetPaintedResources(this Manifest dbManifest, IPathGenerator pathGenerator,
        Dictionary<string, JObject>? assets)
    {
        if (dbManifest.CanvasPaintings.IsNullOrEmpty()) return null;

        return dbManifest.CanvasPaintings
            .OrderBy(cp => cp.CanvasOrder)
            .ThenBy(cp => cp.ChoiceOrder)
            .Select(cp => new PaintedResource
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

        if (assets is null)
            return new()
            {
                ["@id"] = fullAssetId,
                ["error"] = "Unable to retrieve asset details"
            };

        return assets.TryGetValue(fullAssetId, out var asset)
            ? asset
            : new()
            {
                ["@id"] = fullAssetId,
                ["error"] = "Asset not found"
            };
    }
    
    private static IngestingAssets? GenerateIngesting(Dictionary<string, JObject>? assets)
    {
        if (assets == null) return null;
        
        var ingesting = new IngestingAssets();

        foreach (var asset in assets)
        {
            ingesting.Total++;

            if (asset.Value.TryGetValue("ingesting", out var currentlyIngesting))
            {
                if (!currentlyIngesting.Value<bool>())
                {
                    ingesting.Finished++;
                }
            }

            if (!asset.Value.TryGetValue("error", out var error)) continue;
            if (!string.IsNullOrEmpty(error.Value<string>()))
            {
                ingesting.Errors++;
            }
        }
        
        return ingesting;
    }
}
