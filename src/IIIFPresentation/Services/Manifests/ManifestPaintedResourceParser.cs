using Core.Helpers;
using Microsoft.Extensions.Logging;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository.Paths;
using CanvasPainting = Models.Database.CanvasPainting;

namespace Services.Manifests;

/// <summary>
/// Contains logic for parsing a Manifests "paintedResources" property into <see cref="CanvasPainting"/> entities
/// </summary>
public class ManifestPaintedResourceParser(ILogger<ManifestItemsParser> logger) : ICanvasPaintingParser
{
    public IEnumerable<CanvasPainting> ParseToCanvasPainting(PresentationManifest presentationManifest, int customerId)
    {
        if (presentationManifest.PaintedResources.IsNullOrEmpty()) return [];
        
        var paintedResources = presentationManifest.PaintedResources;
        var canvasPaintings = new List<CanvasPainting>();

        using var logScope = logger.BeginScope("Manifest {ManifestId}", presentationManifest.Id);

        var count = 0;
        foreach (var paintedResource in paintedResources)
        {
            if (paintedResource.Asset == null)
            {
                logger.LogInformation("Manifest {ManifestId}:{Customer}, index {Index} ignored as no asset",
                    presentationManifest.Id, customerId, count);
                continue;
            }
            
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
            Ingesting = payloadCanvasPainting.Ingesting,
            StaticWidth = payloadCanvasPainting?.StaticWidth,
            StaticHeight = payloadCanvasPainting?.StaticHeight,
            Duration = payloadCanvasPainting?.Duration,
            Target = payloadCanvasPainting?.Target,
            Thumbnail = payloadCanvasPainting?.Thumbnail == null
                ? null
                : Uri.TryCreate(payloadCanvasPainting.Thumbnail, UriKind.Absolute, out var thumbnail)
                    ? thumbnail
                    : null
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
