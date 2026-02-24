using Core.Exceptions;
using Core.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository.Paths;
using Services.Manifests.Exceptions;
using Services.Manifests.Helpers;
using Services.Manifests.Model;
using Services.Manifests.Settings;
using CanvasPainting = Models.Database.CanvasPainting;

namespace Services.Manifests;

/// <summary>
/// Contains logic for parsing a Manifests "paintedResources" property into <see cref="CanvasPainting"/> entities
/// </summary>
public class ManifestPaintedResourceParser(
    IPathRewriteParser pathRewriteParser, 
    IPresentationPathGenerator presentationPathGenerator,
    IOptions<PathSettings> options,
    ILogger<ManifestPaintedResourceParser> logger)
{
    private readonly PathSettings settings = options.Value;
    
    public IEnumerable<InterimCanvasPainting> ParseToCanvasPainting(PresentationManifest presentationManifest, int customerId)
    {
        if (presentationManifest.PaintedResources.IsNullOrEmpty()) return [];
        
        var paintedResources = presentationManifest.PaintedResources;
        var canvasPaintings = new List<InterimCanvasPainting>();

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
            var implicitOrdering = paintedResource.CanvasPainting?.CanvasOrder == null;
            
            var cp = CreatePartialCanvasPainting(customerId, paintedResource, canvasOrder, implicitOrdering);

            count++;
            canvasPaintings.Add(cp);
        }
        
        return canvasPaintings;
    }
    
    private InterimCanvasPainting CreatePartialCanvasPainting(int customerId, PaintedResource paintedResource,
        int canvasOrder, bool implicitOrdering)
    {
        var specifiedCanvasId = TryGetValidCanvasId(customerId, paintedResource);
        var payloadCanvasPainting = paintedResource.CanvasPainting;
        var (space, assetId) =
            GetCanvasPaintingDetailsForAsset(paintedResource.Asset.ThrowIfNull(nameof(paintedResource.Asset)));

        if (space < 0)
        {
            throw new AssetException(
                $"The space for asset '{assetId}' {(specifiedCanvasId != null ? $"with canvas id '{specifiedCanvasId}' " : "")}is '{space}' and cannot be negative",
                assetId);
        }
        
        logger.LogTrace("Processing canvas painting for asset {AssetId}", assetId);
        var cp = new InterimCanvasPainting
        {
            Label = payloadCanvasPainting?.Label,
            CanvasLabel = payloadCanvasPainting?.CanvasLabel,
            CanvasOrder = canvasOrder,
            SuspectedAssetId = assetId,
            SuspectedSpace = space,
            ChoiceOrder = payloadCanvasPainting?.ChoiceOrder,
            Ingesting = payloadCanvasPainting?.Ingesting ?? false,
            StaticWidth = payloadCanvasPainting?.StaticWidth,
            StaticHeight = payloadCanvasPainting?.StaticHeight,
            Duration = payloadCanvasPainting?.Duration,
            Target = payloadCanvasPainting?.Target,
            CustomerId = customerId,
            CanvasPaintingType = CanvasPaintingType.PaintedResource,
            CanvasOriginalId = payloadCanvasPainting?.CanvasOriginalId != null ? 
                CanvasOriginalHelper.TryGetValidCanvasOriginalId(presentationPathGenerator, customerId, payloadCanvasPainting.CanvasOriginalId) 
                : null,
            Thumbnail = payloadCanvasPainting?.Thumbnail == null
                ? null
                : Uri.TryCreate(payloadCanvasPainting.Thumbnail, UriKind.Absolute, out var thumbnail)
                    ? thumbnail
                    : null,
            ImplicitOrder = implicitOrdering
        };

        if (specifiedCanvasId != null)
        {
            cp.Id = specifiedCanvasId;
        }

        return cp;
    }

    private string? TryGetValidCanvasId(int customerId, PaintedResource paintedResource)
    {
        paintedResource.CanvasPainting ??= new();

        var canvasPainting = paintedResource.CanvasPainting;
        
        if (canvasPainting.CanvasId == null) return null;

        if (!Uri.TryCreate(canvasPainting.CanvasId, UriKind.Absolute, out var canvasId))
        {
            CanvasHelper.CheckForProhibitedCharacters(canvasPainting.CanvasId, logger);
            return canvasPainting.CanvasId;
        }

        if (!settings.IsCustomerRecognisedHost(customerId, canvasId.Host)) 
        {
            throw new InvalidCanvasIdException(canvasPainting.CanvasId,
                $"The host for canvas id '{canvasPainting.CanvasId}' could not be recognised");
        }
        
        var parsedCanvasId =
            pathRewriteParser.ParsePathWithRewrites(canvasId.Host, canvasId.AbsolutePath, customerId);
        CanvasHelper.CheckParsedCanvasIdForErrors(parsedCanvasId, canvasId.AbsolutePath, logger);
        
        if (customerId != parsedCanvasId.Customer)
        {
            throw new InvalidCanvasIdException(canvasPainting.CanvasId,
                $"The customer parsed from the canvas id does not match the customer found from the calling URL");
        }

        return parsedCanvasId.Resource;
    }
    
    private static (int? space, string id) GetCanvasPaintingDetailsForAsset(JObject asset)
    {
        // Read props from Asset - id must be there. If not, throw an exception
        var space = asset.TryGetValue<int?>(AssetProperties.Space);
        var id = asset.GetRequiredValue<string>(AssetProperties.Id);
        return (space, id);
    }
}
