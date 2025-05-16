using Core.Helpers;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository.Paths;
using CanvasPainting = Models.Database.CanvasPainting;

namespace Repository.Manifests;

/// <summary>
/// Contains lgic for parsing a Manifests "paintedResources" property into <see cref="CanvasPainting"/> entities
/// </summary>
public class ManifestPaintedResourceParser(ILogger<ManifestItemsParser> logger)
{
    public IEnumerable<CanvasPainting> ParseItemsToCanvasPainting(PresentationManifest presentationManifest, int customerId)
    {
        if (presentationManifest.PaintedResources.IsNullOrEmpty()) return [];
        
        var paintedResources = presentationManifest.PaintedResources;
        var canvasPaintings = new List<CanvasPainting>();
        var count = 0;

        using var logScope = logger.BeginScope("Manifest {ManifestId}", presentationManifest.Id);
        
        foreach (var paintedResource in paintedResources)
        {
            // TODO - should this throw?
            if (paintedResource.Asset == null) continue;
            
            var canvasOrder = paintedResource.CanvasPainting?.CanvasOrder ?? count;
            
            var cp = CreatePartialCanvasPainting(customerId, paintedResource, canvasOrder);

            count++;
            canvasPaintings.Add(cp);
        }
        
        return canvasPaintings;
    }

    private CanvasPainting CreatePartialCanvasPainting(int customerId, PaintedResource paintedResource,
        int canvasOrder)
    {
        var specifiedCanvasId = TryGetValidCanvasId(customerId, paintedResource);
        var payloadCanvasPainting = paintedResource.CanvasPainting;
        var assetId = GetAssetIdForAsset(paintedResource.Asset!, customerId);
        logger.LogTrace("Processing canvas painting for asset {AssetId}", assetId);
        var cp = new CanvasPainting
        {
            Label = payloadCanvasPainting?.Label,
            CanvasLabel = payloadCanvasPainting?.CanvasLabel,
            CanvasOrder = canvasOrder,
            AssetId = assetId,
            ChoiceOrder = payloadCanvasPainting?.ChoiceOrder,
            Ingesting = true,
            StaticWidth = payloadCanvasPainting?.StaticWidth,
            StaticHeight = payloadCanvasPainting?.StaticHeight,
            Duration = payloadCanvasPainting?.Duration,
        };

        if (specifiedCanvasId != null)
        {
            cp.Id = specifiedCanvasId;
        }

        return cp;
    }

    private static string? TryGetValidCanvasId(int customerId, PaintedResource paintedResource)
    {
        paintedResource.CanvasPainting ??= new();
        var canvasId = GetCanvasId(customerId, paintedResource.CanvasPainting);
        return canvasId;
    }

    // PK: Simplified by #340
    private static string? GetCanvasId(int customerId, Models.API.Manifest.CanvasPainting canvasPainting)
        => canvasPainting.CanvasId != null ? PathParser.GetCanvasId(canvasPainting, customerId) : null;

    private static AssetId GetAssetIdForAsset(JObject asset, int customerId)
    {
        // Read props from Asset - these must be there. If not, throw an exception
        var space = asset.GetRequiredValue(AssetProperties.Space);
        var id = asset.GetRequiredValue(AssetProperties.Id);
        return AssetId.FromString($"{customerId}/{space}/{id}");
    }
}
