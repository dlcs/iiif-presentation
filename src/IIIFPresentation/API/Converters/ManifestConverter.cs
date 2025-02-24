using AWS.Helpers;
using Core.Helpers;
using Core.IIIF;
using DLCS.Models;
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
        iiifManifest.Space = pathGenerator.GenerateSpaceUri(dbManifest)?.ToString();

        if (dbManifest.IsIngesting())
        {
            iiifManifest.Ingesting = GenerateIngesting(assets);
        }
        
        var canvasPaintings = dbManifest.GetOrderedCanvasPaintings();
        if (canvasPaintings is not null)
        {
            var enumeratedCanvasPaintings = canvasPaintings.ToList();
            iiifManifest.PaintedResources = enumeratedCanvasPaintings.GetPaintedResources(pathGenerator, assets);

            // Note ??= - this is only if we don't yet have Items set by background process
            iiifManifest.Items ??= enumeratedCanvasPaintings.GenerateProvisionalItems(pathGenerator);
        }
        
        iiifManifest.EnsurePresentation3Context();
        iiifManifest.EnsureContext(PresentationJsonLdContext.Context);
        iiifManifest.RemovePresentationBehaviours();
        
        return iiifManifest;
    }

    /// <summary>
    /// Generate provisional <see cref="Canvas"/> items from provided <see cref="CanvasPainting"/> collection. These
    /// provisional canvases have the structure of the final canvases without the full content-resource details
    /// </summary>
    private static List<Canvas> GenerateProvisionalItems(this IList<CanvasPainting> canvasPaintings,
        IPathGenerator pathGenerator)
    {
        return canvasPaintings
            .GroupBy(pr => pr.CanvasOrder)
            .Select(GenerateProvisionalCanvas)
            .ToList();

        Canvas GenerateProvisionalCanvas(IGrouping<int, CanvasPainting> groupedCanvasPaintings)
        {
            var canvasPainting = groupedCanvasPaintings.First();
            var canvasId = pathGenerator.GenerateCanvasId(canvasPainting); 
            var c = new Canvas
            {
                Id = canvasId,
                Items =
                [
                    new()
                    {
                        Id = pathGenerator.GenerateAnnotationPagesId(canvasPainting),
                        Items =
                        [
                            new PaintingAnnotation
                            {
                                Id = pathGenerator.GeneratePaintingAnnotationId(canvasPainting),
                                Behavior = [Behavior.Processing],
                                Target = new Canvas {Id = canvasId},
                                Body = GetBody(groupedCanvasPaintings)
                            }
                        ]
                    }
                ]
            };

            return c;
        }

        IPaintable GetBody(IGrouping<int, CanvasPainting> paintings)
        {
            if (paintings.Count() > 1)
                return new PaintingChoice
                {
                    Items = paintings.OrderBy(p => p.ChoiceOrder ?? -1).Select(p => GetImage()).ToList()
                };

            return GetImage();
        }

        IPaintable GetImage() => new Image();
    }

    private static IOrderedEnumerable<CanvasPainting>? GetOrderedCanvasPaintings(this Manifest dbManifest)
    {
        if (dbManifest.CanvasPaintings.IsNullOrEmpty()) return null;

        return dbManifest.CanvasPaintings
            .OrderBy(cp => cp.CanvasOrder)
            .ThenBy(cp => cp.ChoiceOrder);
    }

    private static List<PaintedResource> GetPaintedResources(this IList<CanvasPainting> canvasPaintings, IPathGenerator pathGenerator,
        Dictionary<string, JObject>? assets)
    {
        return canvasPaintings
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
        if (cp.AssetId == null) return null;

        var fullAssetId = pathGenerator.GenerateAssetUri(cp)?.ToString();
        if (fullAssetId == null) return null;

        if (assets is null)
            return new()
            {
                [AssetProperties.FullId] = fullAssetId,
                [AssetProperties.Error] = "Unable to retrieve asset details"
            };

        return assets.TryGetValue(fullAssetId, out var asset)
            ? asset
            : new()
            {
                [AssetProperties.FullId] = fullAssetId,
                [AssetProperties.Error] = "Asset not found"
            };
    }
    
    private static IngestingAssets? GenerateIngesting(Dictionary<string, JObject>? assets)
    {
        if (assets == null) return null;
        
        var ingesting = new IngestingAssets();

        foreach (var asset in assets)
        {
            ingesting.Total++;

            if (asset.Value.TryGetValue(AssetProperties.Ingesting, out var currentlyIngesting))
            {
                if (!currentlyIngesting.Value<bool>())
                {
                    ingesting.Finished++;
                }
            }

            if (!asset.Value.TryGetValue(AssetProperties.Error, out var error)) continue;
            if (!string.IsNullOrEmpty(error.Value<string>()))
            {
                ingesting.Errors++;
            }
        }
        
        return ingesting;
    }
}
