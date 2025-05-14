using Core.Helpers;
using Core.IIIF;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Serialisation;
using Models.Database;

namespace Repository.Manifests;

/// <summary>
/// Contains logic for parsing a Manifests "items" property into <see cref="CanvasPainting"/> entities
/// </summary>
public class ManifestItemsParser(ILogger<ManifestItemsParser> logger)
{
    public IEnumerable<CanvasPainting> ParseItemsToCanvasPainting(Manifest manifest)
    {
        if (manifest.Items.IsNullOrEmpty()) return [];

        using var logScope = logger.BeginScope("Manifest {ManifestId}", manifest.Id);
        
        var canvasPaintings = new List<CanvasPainting>(manifest.Items.Count);
        int canvasOrder = 0;
        
        foreach (var canvas in manifest.Items)
        {
            logger.LogTrace("Processing canvas {CanvasOrder}:'{CanvasId}'...", canvasOrder, canvas.Id);
            
            var canvasLabelHasBeenSet = false;
            foreach (var painting in canvas.GetPaintingAnnotations())
            {
                var target = painting.Target;

                var body = painting.Body;
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
                                canvas, choiceOrder);

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

                    var cp = CreatePartialCanvasPainting(resource, canvas.Id, canvasOrder, target, canvas);

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

    private CanvasPainting CreatePartialCanvasPainting(ResourceBase resource,
        string? canvasOriginalId,
        int canvasOrder,
        IStructuralLocation? target,
        Canvas currentCanvas,
        int? choiceOrder = null)
    {
        // Create "partial" canvasPaintings that only contains values derived from manifest (no customer, manifest etc) 
        var cp = new CanvasPainting
        {
            CanvasOriginalId = string.IsNullOrEmpty(canvasOriginalId) ? null : new Uri(canvasOriginalId),
            CanvasOrder = canvasOrder,
            ChoiceOrder = choiceOrder,
            Target = TargetAsString(target, currentCanvas),
            Thumbnail = TryGetThumbnail(currentCanvas),
        };
        
        if (resource is ISpatial spatial)
        {
            cp.StaticWidth = spatial.Width;
            cp.StaticHeight = spatial.Height;
        }
        return cp;
    }
    
    private static Uri? TryGetThumbnail(Canvas canvas)
    {
        if (canvas.Thumbnail.IsNullOrEmpty())
            return null;

        var thumbnail = canvas.Thumbnail.OfType<Image>().GetThumbnailPath();
        return Uri.TryCreate(thumbnail, UriKind.Absolute, out var thumbnailUri) ? thumbnailUri : null;
    }

    private static string? TargetAsString(IStructuralLocation? target, Canvas currentCanvas)
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
