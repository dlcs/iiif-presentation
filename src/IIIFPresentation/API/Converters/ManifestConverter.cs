using Core.Helpers;
using Core.IIIF;
using Core.Infrastructure;
using IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using Models.API.Manifest;
using Models.Database.Collections;
using Models.Database.General;
using Models.DLCS;
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

            iiifManifest.Items = enumeratedCanvasPaintings.GenerateProvisionalItems(pathGenerator, iiifManifest.Items);
        }
        
        iiifManifest.EnsurePresentation3Context();
        iiifManifest.EnsureContext(PresentationJsonLdContext.Context);
        
        return iiifManifest;
    }
    
    /// <summary>
    /// Generate provisional <see cref="Canvas"/> items from provided <see cref="CanvasPainting"/> collection. These
    /// provisional canvases have the structure of the final canvases without the full content-resource details
    /// </summary>
    private static List<Canvas> GenerateProvisionalItems(this IList<CanvasPainting> canvasPaintings,
        IPathGenerator pathGenerator, List<Canvas>? items)
    {
        items ??= [];
        
        // ToLookup, rather than GroupBy - the former maintains order of input. The latter orders by key.
        // We need to maintain order by CanvasOrder > ChoiceOrder, NOT canvasId (even though we are grouping by that)
        return canvasPaintings
            .ToLookup(pr => pr.Id)
            .Select(g => GenerateProvisionalCanvas(g, items))
            .ToList();

        Canvas GenerateProvisionalCanvas(IGrouping<string, CanvasPainting> groupedCanvasPaintings, List<Canvas> itemsInCanvas)
        {
            // Incoming grouping is by canvasId - so could contain 1:n paintingAnnos, some of which are choices
            var canvasPainting = groupedCanvasPaintings.First();
            var canvasId = pathGenerator.GenerateCanvasId(canvasPainting);
            
            // check and find the attached item, if it exists and fallback to seeing if we have an item based on the canvas id
            // this is used when either we already have an item we're generating a PR from, OR when we can build the manifest immediately
            var item = itemsInCanvas.FirstOrDefault(
                i => canvasPainting.CanvasOriginalId?.ToString() == i.Id || canvasId == i.Id);
            if (item != null) return item;

            var c = new Canvas
            {
                Id = canvasId,
                Items =
                [
                    new()
                    {
                        Id = pathGenerator.GenerateAnnotationPagesId(canvasPainting),
                        
                        // To get the correct "items" group by CanvasOrder. Those that share order are a choice
                        Items = groupedCanvasPaintings
                            .GroupBy(cp => cp.CanvasOrder)
                            .Select(orderCanvasPaintings =>
                            {
                                // We are in grouping by CanvasOrder, if choice there may be > 1 canvasPainting. Ids at 
                                // this level will be same for matching CanvasOrder so attempt to find one with a target
                                // as that can change "target" value. If there are multiple with a .Target then take the
                                // lowest choice
                                var canvasPaintingForId = orderCanvasPaintings
                                    .OrderBy(cp => string.IsNullOrEmpty(cp.Target))
                                    .ThenBy(cp => cp.ChoiceOrder ?? 0)
                                    .First();
                                var annotation = new PaintingAnnotation
                                {
                                    Id = pathGenerator.GeneratePaintingAnnotationId(canvasPaintingForId),
                                    Behavior = [Behavior.Processing],
                                    Target = new Canvas
                                        { Id = pathGenerator.GenerateCanvasIdWithTarget(canvasPaintingForId) },
                                    Body = GetBody(orderCanvasPaintings)
                                };
                                return annotation;
                            }).Cast<IAnnotation>()
                            .ToList()
                    }
                ]
            };

            return c;
        }

        IPaintable? GetBody(IGrouping<int, CanvasPainting> paintings) =>
            paintings.Count() > 1 ? new PaintingChoice() : null;
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
