using Core.Exceptions;
using Core.Helpers;
using Core.IIIF;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Traversal;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Models.DLCS;
using Repository.Paths;
using Services.Manifests.Helpers;
using Services.Manifests.Model;
using Services.Manifests.Settings;
using CanvasPainting = Models.Database.CanvasPainting;

namespace Services.Manifests;

/// <summary>
/// Contains logic for parsing a Manifests "items" property into <see cref="CanvasPainting"/> entities
/// </summary>
public class ManifestItemsParser(
    IPathRewriteParser pathRewriteParser,
    IPresentationPathGenerator presentationPathGenerator,
    PaintableAssetIdentifier paintableAssetIdentifier,
    IOptions<PathSettings> options,
    ILogger<ManifestItemsParser> logger)
{
    private readonly PathSettings settings = options.Value;
    
    public IEnumerable<InterimCanvasPainting> ParseToCanvasPainting(PresentationManifest manifest, 
        List<InterimCanvasPainting> paintedResourceCanvasPainting, int customer)
    {
        if (manifest.Items.IsNullOrEmpty()) return [];

        using var logScope = logger.BeginScope("Manifest {ManifestId}", manifest.Id);
        
        var canvasPaintings = new List<InterimCanvasPainting>(manifest.Items.Count);
        int canvasOrder = 0;
        
        foreach (var canvas in manifest.Items)
        {
            logger.LogTrace("Processing canvas {CanvasOrder}:'{CanvasId}'...", canvasOrder, canvas.Id);

            var canvasPaintingsInCanvas = canvas.GetPaintingAnnotations().ToList();

            // in this case, we have an item with no painting annotation, so it could be tracked by a painted resource
            if (canvasPaintingsInCanvas.Count == 0)
            {
                var cp = CreatePartialCanvasPainting(null, canvas.Id, canvasOrder, null, canvas, customer, paintedResourceCanvasPainting);
                cp.CanvasLabel = canvas.Label;
                canvasPaintings.Add(cp);
                canvasOrder++;
                continue;
            }
            
            var identifiedManagedAssets = manifest
                .AllPaintingAnnoBodies()
                .Select(paintable =>(paintable,  assetId: paintableAssetIdentifier.ResolvePaintableAsset(paintable, customer)))
                .Where(tuple => tuple.assetId != null)
                .ToDictionary();
            
            var canvasLabelHasBeenSet = false;
            foreach (var painting in canvasPaintingsInCanvas)
            {
                var target = painting.Target;

                var body = painting.Body;
                var assetId = body is not null && identifiedManagedAssets.TryGetValue(body, out var resolvedId) ? resolvedId : null;
                
                if (body is PaintingChoice choice)
                {
                    logger.LogTrace("Canvas {CanvasOrder}:'{CanvasId}' is a choice", canvasOrder, canvas.Id);
                    var choiceCanvasOrder = canvasOrder;
                    var first = true;

                    // (not -1; "a positive integer indicates that the asset is part of a Choice body.")
                    var choiceOrder = 1;

                    foreach (var choiceItem in choice.Items ?? [])
                    {
                        var resource = GetResourceFromBody(choiceItem);

                        if (resource is Image or Video or Sound)
                        {
                            var cp = CreatePartialCanvasPainting(resource, canvas.Id, choiceCanvasOrder, target,
                                canvas, customer, paintedResourceCanvasPainting, choiceOrder, assetId);

                            choiceOrder++;
                            if (first)
                            {
                                canvasOrder++;
                                first = false;
                            }

                            target = null; // don't apply it to subsequent members of the choice

                            cp.Label = resource.Label ?? painting.Label ?? canvas.Label;
                            if (!canvasLabelHasBeenSet && canvas.Label != null && canvas.Label != cp.Label)
                            {
                                cp.CanvasLabel = canvas.Label;
                                canvasLabelHasBeenSet = true;
                            }
                            
                            canvasPaintings.Add(cp);
                        }
                        else
                        {
                            // body could be a Canvas - will need to handle that eventually but not right now
                            // It is handled by unpacking the canvas into another loop through this
                            logger.LogError("Canvas {CanvasOrder}:'{CanvasId}' not supported", canvasOrder, canvas.Id);
                            throw new NotImplementedException("Support for canvases as painting anno bodies not implemented");
                        }
                    }
                }
                else
                {
                    var resource = GetResourceFromBody(body);

                    if (resource == null)
                    {
                        logger.LogTrace("Canvas {CanvasOrder}:'{CanvasId}' is unsupported body", canvasOrder,
                            canvas.Id);
                        throw new InvalidOperationException(
                            $"Body type '{body}' not supported as painting annotation body");
                    }

                    var cp = CreatePartialCanvasPainting(resource, canvas.Id, canvasOrder, target, canvas, customer, paintedResourceCanvasPainting, assetId: assetId);

                    canvasOrder++;
                    cp.Label = resource.Label ?? painting.Label ?? canvas.Label;
                    if (!canvasLabelHasBeenSet && canvas.Label != null && canvas.Label != cp.Label)
                    {
                        cp.CanvasLabel = canvas.Label;
                        canvasLabelHasBeenSet = true;
                    }
                    canvasPaintings.Add(cp);
                }
            }
        }

        return canvasPaintings;
    }
    
    private static ResourceBase? GetResourceFromBody(IPaintable? body)
    {
        var resource = body as ResourceBase;
        if (resource is SpecificResource specificResource)
        {
            resource = specificResource.Source as ResourceBase;
        }

        return resource;
    }
    
    private InterimCanvasPainting CreatePartialCanvasPainting(ResourceBase? resource,
        string? canvasOriginalId,
        int canvasOrder,
        ResourceBase? target,
        Canvas currentCanvas,
        int customerId,
        List<InterimCanvasPainting>? paintedResourceCanvasPaintings,
        int? choiceOrder = null,
        AssetId? assetId = null)
    {
        // Create "partial" canvasPaintings that only contains values derived from manifest (no customer, manifest etc) 
        var cp = new InterimCanvasPainting
        {
            CanvasOriginalId = CanvasOriginalHelper.TryGetValidCanvasOriginalId(presentationPathGenerator, customerId, canvasOriginalId), 
            CanvasOrder = canvasOrder,
            ChoiceOrder = choiceOrder,
            Target = TargetAsString(target, currentCanvas),
            Thumbnail = TryGetThumbnail(currentCanvas),
            CanvasPaintingType = CanvasPaintingType.Items,
            ImplicitOrder = true,
            CustomerId = customerId
        };
        
        if (resource is ISpatial spatial)
        {
            cp.StaticWidth = spatial.Width;
            cp.StaticHeight = spatial.Height;
        }

        var canvasId = TryGetValidCanvasId(customerId, currentCanvas, paintedResourceCanvasPaintings);
        if (canvasId != null)
        {
            cp.Id = canvasId;
        }

        if (assetId != null)
        {
            cp.AssetId = assetId.Asset;
            cp.Space = assetId.Space;
        }
        
        return cp;
    }
    
    private string? TryGetValidCanvasId(int customerId, Canvas currentCanvas, 
        List<InterimCanvasPainting>? paintedResourceCanvasPaintings)
    {
        if (currentCanvas.Id == null) return null;

        if (!Uri.TryCreate(currentCanvas.Id, UriKind.Absolute, out var canvasId))
        {
            currentCanvas.Id = CanvasHelper.CheckForProhibitedCharacters(currentCanvas.Id, logger, false);
            
            if (currentCanvas.Id != null && !paintedResourceCanvasPaintings.InterimCanvasPaintingContainsId(currentCanvas.Id))
            {
                throw new InvalidCanvasIdException(currentCanvas.Id, "The canvas id is not a valid URI, and cannot be matched with a painted resource");
            }
            
            return currentCanvas.Id;
        }

        if (IsRecognisedHost(customerId, canvasId.Host))
        {
            var parsedCanvasId =
                pathRewriteParser.ParsePathWithRewrites(canvasId.Host, canvasId.AbsolutePath, customerId);

            var checkedCanvasId =
                CanvasHelper.CheckParsedCanvasIdForErrors(parsedCanvasId, canvasId.AbsolutePath, logger, false);
            
            if (paintedResourceCanvasPaintings.InterimCanvasPaintingContainsId(checkedCanvasId))
            {
                return checkedCanvasId;
            }
        }

        return null;
    }
    
    /// <summary>
    /// Whether the host is either a customer specific host, or the standard presentation host URL
    /// </summary>
    private bool IsRecognisedHost(int customerId, string host) =>
        settings.GetCustomerSpecificPresentationUrl(customerId).Host == host || settings.PresentationApiUrl.Host == host;

    private static Uri? TryGetThumbnail(Canvas canvas)
    {
        if (canvas.Thumbnail.IsNullOrEmpty())
            return null;

        var thumbnail = canvas.Thumbnail.OfType<Image>().GetThumbnailPath();
        return Uri.TryCreate(thumbnail, UriKind.Absolute, out var thumbnailUri) ? thumbnailUri : null;
    }

    private static string? TargetAsString(ResourceBase? target, Canvas currentCanvas)
    {
        switch (target)
        {
            case null:
            case Canvas canvas when currentCanvas.Id == canvas.Id:
                // This indicates that we are targeting the whole canvas
                return null;
            case Canvas canvas:
                return canvas.Id;
            case SpecificResource specificResource:
                return specificResource.AsJson();
            default:
                return null;
        }
    }
}

public static class ManifestX
{
    /// <summary>
    /// Get all <see cref="PaintingAnnotation"/> from canvas. This is a convenience method to avoid multiple nested
    /// loops 
    /// </summary>
    public static IEnumerable<PaintingAnnotation> GetPaintingAnnotations(this Canvas canvas)
    {
        foreach (var annoPage in canvas.Items ?? [])
        {
            foreach (var anno in annoPage.Items ?? [])
            {
                if (anno is PaintingAnnotation painting)
                {
                    yield return painting;
                }
            }
        }
    }
}
