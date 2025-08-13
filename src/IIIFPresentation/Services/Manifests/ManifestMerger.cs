using System.Diagnostics;
using Core.Helpers;
using Core.IIIF;
using IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Microsoft.Extensions.Logging;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository.Paths;
using Services.Manifests.Helpers;
using CanvasPainting = Models.Database.CanvasPainting;

namespace Services.Manifests;

public interface IManifestMerger
{
    /// <summary>
    /// Build final Manifest using results of NamedQuery manifest for content resources and CanvasPaintings for
    /// instructions.
    /// CanvasPaintings may be updated as part of processing
    /// </summary>
    /// <param name="baseManifest">Initial manifest to project content resources onto</param>
    /// <param name="namedQueryManifest">NamedQuery manifest from DLCS, containing content resources</param>
    /// <param name="canvasPaintings">
    /// <see cref="CanvasPainting"/> records with instructions for how to populated final manifest
    /// </param>
    /// <returns>Populated manifest</returns>
    Manifest ProcessCanvasPaintings(Manifest baseManifest, Manifest? namedQueryManifest,
        List<CanvasPainting>? canvasPaintings);
}

public class ManifestMerger(IPathGenerator pathGenerator, ILogger<ManifestMerger> logger) : IManifestMerger
{
    /// <summary>
    /// Process specified <see cref="CanvasPainting"/> objects to project contents from namedQueryManifest onto the
    /// provided baseManifest.
    /// CanvasPaintings are updated as part of processing
    /// </summary>
    /// <param name="baseManifest">Initial manifest to project content resources onto</param>
    /// <param name="namedQueryManifest">NamedQuery manifest from DLCS, containing content resources</param>
    /// <param name="canvasPaintings">
    /// <see cref="CanvasPainting"/> records with instructions for how to populated final manifest
    /// </param>
    /// <returns>Populated manifest</returns>
    public Manifest ProcessCanvasPaintings(Manifest baseManifest, Manifest? namedQueryManifest,
        List<CanvasPainting>? canvasPaintings)
    {
        ValidateManifests(baseManifest, namedQueryManifest);

        if (canvasPaintings.IsNullOrEmpty())
        {
            logger.LogInformation("No canvas paintings found for manifest {ManifestId}", baseManifest.Id);
            return baseManifest;
        }

        var canvasDictionary = BuildAssetIdToCanvasLookup(namedQueryManifest!);
        BuildItems(baseManifest, canvasPaintings, canvasDictionary);
        SetManifestContext(baseManifest, namedQueryManifest!);

        return baseManifest;
    }
    
    private void ValidateManifests(Manifest baseManifest, Manifest? namedQueryManifest)
    {
        // Ensure NQ has items or we can't do anything
        if (namedQueryManifest?.Items == null && baseManifest.Items == null)
        {
            logger.LogWarning("NamedQuery Manifest '{ManifestId}' null or missing items",
                namedQueryManifest?.Id ?? "no-id");
            throw new ArgumentNullException("namedQueryManifest.Items");
        }
    }

    private Dictionary<AssetId, Canvas> BuildAssetIdToCanvasLookup(Manifest namedQueryManifest)
    {
        try
        {
            return namedQueryManifest
                .Items!
                .ToDictionary(canvas => canvas.GetAssetIdFromNamedQueryCanvasId(), canvas => canvas);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error building Asset:Canvas lookup for {ManifestId}", namedQueryManifest?.Id);
            throw;
        }
    }
    
    /// <summary>
    /// Merges a generated DLCS manifest with the current manifest in S3
    /// </summary>
    private void BuildItems(Manifest baseManifest, List<CanvasPainting> canvasPaintings, 
        Dictionary<AssetId, Canvas> canvasDictionary)
    {
        // Get the canvasPaintings in the order we want to process them (Canvas => Choice) but group by CanvasId as
        // canvases with differing orders can share an id
        var canvasGrouping = canvasPaintings
            .OrderBy(cp => cp.CanvasOrder)
            .ThenBy(cp => cp.ChoiceOrder ?? 0)
            .ToLookup(cp => cp.Id, cp => cp);
        
        logger.LogDebug("Processing {CanvasCount} canvases on Manifest {ManifestId}", canvasGrouping.Count,
            baseManifest.Id);

        var items = new List<Canvas>();
        
        // Each grouping provides an 'instruction' on how to paint the CanvasPaintings onto canvas.
        // All canvasPaintings in single grouping will be on single canvas
        foreach (var canvasInstruction in canvasGrouping)
        {
            var singleItemCanvas = canvasInstruction.Count() == 1;
            logger.LogTrace("Processing {CanvasId}. SingleItem: {SingleItemCanvas}", canvasInstruction.Key,
                singleItemCanvas);

            var canvas = GenerateCanvas(canvasDictionary, canvasInstruction, baseManifest.Items, singleItemCanvas);

            items.Add(canvas);
        }
        
        baseManifest.Items = items;
    }

    private Canvas GenerateCanvas(Dictionary<AssetId, Canvas> canvasDictionary,
        IGrouping<string, CanvasPainting> canvasInstruction, List<Canvas> items, bool singleItemCanvas)
    {
        // Instruction is grouped by canvasId, so any can be used to generate canvas level ids
        var firstCanvasPaintingInCanvas = canvasInstruction.First();

        // Instantiate a new Canvas, this is what we are building
        var canvas = new Canvas { Id = pathGenerator.GenerateCanvasId(firstCanvasPaintingInCanvas), };

        // Instantiate a new AnnotationPage - this is what we'll populate with PaintingAnnotations below
        var annoPage = new AnnotationPage
        {
            Id = pathGenerator.GenerateAnnotationPagesId(firstCanvasPaintingInCanvas),
            Items = []
        };
        
        // Iterate through the canvasPaintings to be rendered on this Canvas. Group by CanvasOrder as:
        // multiple orders can share an Id (for composite)
        // each CanvasOrder might have multiple items (for choice) 
        foreach (var canvasOrderGroup in canvasInstruction.GroupBy(ci => ci.CanvasOrder))
        {
            var canvasOrderCount = canvasOrderGroup.Count();
            logger.LogTrace("Processing Canvas {CanvasId}, contains {Count} items", canvasInstruction.Key,
                canvasOrderCount);

            var isChoice = canvasOrderCount > 1;

            var firstCanvasPainting = canvasOrderGroup.First();

            if (firstCanvasPainting.AssetId == null)
            {
                var item = items.FirstOrDefault(i => i.Id == firstCanvasPainting.CanvasOriginalId!.ToString());
                
                return item ?? canvas;
            }
            
            var currentPaintingAnno = new PaintingAnnotation
            {
                Id = pathGenerator.GeneratePaintingAnnotationId(firstCanvasPainting),
            };

            if (!isChoice)
            {
                ProcessNonChoice(canvasDictionary, canvasOrderGroup.Single(), canvas, currentPaintingAnno,
                    singleItemCanvas);
            }
            else
            {
                ProcessChoice(canvasDictionary, canvasOrderGroup, canvas, currentPaintingAnno);
            }

            annoPage.Items!.Add(currentPaintingAnno);
        }

        if (canvas.Label == null)
        {
            logger.LogTrace("Canvas has no label, attempting to set to first non-null Label");
            canvas.Label = canvasInstruction.FirstOrDefault(ci => ci.Label != null)?.Label;
        }
        
        canvas.Items = [annoPage];
        return canvas;
    }

    private void ProcessNonChoice(Dictionary<AssetId, Canvas> canvasDictionary, CanvasPainting canvasPainting,
        Canvas canvas, PaintingAnnotation currentPaintingAnno, bool singleItemCanvas)
    {
        if (!canvasDictionary.TryGetValue(canvasPainting.AssetId!, out var namedQueryCanvas))
        {
            logger.LogWarning(
                "Could not find NQ canvas for Asset {AssetId} from CanvasPainting {CanvasPaintingId}",
                canvasPainting.AssetId, canvasPainting.Id);
            return;
        }

        var body = GetSafeFirstPaintingAnnotationBody(namedQueryCanvas);
        HandleCanvasPainting(canvasPainting, canvas, namedQueryCanvas, body);

        currentPaintingAnno.Target = new Canvas
        {
            Id = pathGenerator.GenerateCanvasIdWithTarget(canvasPainting),
        };

        // If this is a single item canvas then the .Label will be for the Canvas, rather than paintingAnno.
        // UNLESS there's a specific .CanvasLabel as that will already have been set
        if (canvasPainting.Label != null)
        {
            if (singleItemCanvas && canvasPainting.CanvasLabel == null)
            {
                canvas.Label = canvasPainting.Label;
            }
            else
            {
                (body as ResourceBase)!.Label = canvasPainting.Label;
            }
        }

        currentPaintingAnno.Body = body;
    }

    private void ProcessChoice(Dictionary<AssetId, Canvas> canvasDictionary,
        IGrouping<int, CanvasPainting> canvasOrderGroup, Canvas canvas, PaintingAnnotation currentPaintingAnno)
    {
        var paintingChoice = new PaintingChoice { Items = [] };
        foreach (var canvasPaintingChoice in canvasOrderGroup)
        {
            if (!canvasDictionary.TryGetValue(canvasPaintingChoice.AssetId!, out var namedQueryCanvas))
            {
                logger.LogWarning(
                    "Could not find NQ canvas for Asset {AssetId} from CanvasPainting {CanvasPaintingId}",
                    canvasPaintingChoice.AssetId, canvasPaintingChoice.Id);
                continue;
            }
            
            var body = GetSafeFirstPaintingAnnotationBody(namedQueryCanvas);
            HandleCanvasPainting(canvasPaintingChoice, canvas, namedQueryCanvas, body);

            // We might have 1 or more IPaintable elements (e.g. if NQ resource is already a choice, flatten it)
            var paintables = GetPaintablesForChoice(body);
            if (canvasPaintingChoice.Label != null)
            {
                logger.LogTrace("CanvasPainting {CanvasPaintingId} has label, setting on choice paintables",
                    canvasPaintingChoice.Id);
                foreach (var resourceBase in paintables.OfType<ResourceBase>())
                {
                    resourceBase.Label = canvasPaintingChoice.Label;
                }
            }
            paintingChoice.Items.AddRange(paintables);

            // For assets making up a Choice, the first non-null value will be assumed to be the target of
            // the Choice annotation.
            if (canvasPaintingChoice.Target != null && currentPaintingAnno.Target == null)
            {
                logger.LogTrace("Setting target from choice {ChoiceOrder}", canvasPaintingChoice.ChoiceOrder);
                currentPaintingAnno.Target = new Canvas
                {
                    Id = pathGenerator.GenerateCanvasIdWithTarget(canvasPaintingChoice),
                };
            }
        }

        // If we didn't set a target from a choice, set to canvasId (the entire Canvas)
        currentPaintingAnno.Target ??= new Canvas { Id = canvas.Id, };
        currentPaintingAnno.Body = paintingChoice;
        return;

        List<IPaintable> GetPaintablesForChoice(IPaintable body)
            => body is PaintingChoice namedQueryPaintingChoice
                ? namedQueryPaintingChoice.Items!
                : [body];
    }

    private void HandleCanvasPainting(CanvasPainting canvasPainting, Canvas workingCanvas, Canvas namedQueryCanvas,
        IPaintable body)
    {
        // Renderings are accumulated
        if (!namedQueryCanvas.Rendering.IsNullOrEmpty())
        {
            logger.LogTrace("NQ Canvas for AssetId {AssetId} has rendering", canvasPainting.AssetId);
            workingCanvas.Rendering ??= [];
            workingCanvas.Rendering.AddRange(namedQueryCanvas.Rendering);
        }

        // Any custom behaviours are added but only unique values
        if (!namedQueryCanvas.Behavior.IsNullOrEmpty())
        {
            logger.LogTrace("NQ Canvas for AssetId {AssetId} has behaviour", canvasPainting.AssetId);
            workingCanvas.Behavior = (workingCanvas.Behavior ?? []).Union(namedQueryCanvas.Behavior).ToList();
        }

        // Canvas gets the first non-null thumbnail
        if (workingCanvas.Thumbnail.IsNullOrEmpty() && !namedQueryCanvas.Thumbnail.IsNullOrEmpty())
        {
            logger.LogTrace("Using Thumbnail from NQ Canvas for AssetId {AssetId}", canvasPainting.AssetId);
            workingCanvas.Thumbnail = namedQueryCanvas.Thumbnail;
        }

        // Canvas always gets the first non-null canvasPainting.CanvasLabel value, it might get canvasPainting.Label if
        // there is no canvasLabel but logic is dependent on single-item or multi-item canvases (and not handled here!)
        if (workingCanvas.Label == null && canvasPainting.CanvasLabel != null)
        {
            logger.LogTrace("Using CanvasLabel from {CanvasPaintingId} for Canvas.Label", canvasPainting.Id);
            workingCanvas.Label = canvasPainting.CanvasLabel;
        }

        // Use first canvas to set temporal and spatial dimensions - do this with first possible only.
        // Once one dimension is set consider all set
        if (workingCanvas.DimensionsAreUnset())
        {
            workingCanvas.Duration = namedQueryCanvas.Duration;
            workingCanvas.Height = namedQueryCanvas.Height;
            workingCanvas.Width = namedQueryCanvas.Width;
        }
        
        AlignCanvasPaintingAndBody(canvasPainting, namedQueryCanvas, body);
    }

    /// <summary>
    /// Mark CanvasPainting as processed. Also align CanvasPainting and NQ paintable, depending on what CP record
    /// instructs. In some instances we update paintable, in others  
    /// </summary>
    private void AlignCanvasPaintingAndBody(CanvasPainting canvasPainting, Canvas namedQueryCanvas, IPaintable body)
    {
        var thumbnailPath = namedQueryCanvas.Thumbnail?.OfType<Image>().GetThumbnailPath();

        canvasPainting.Thumbnail = thumbnailPath != null ? new Uri(thumbnailPath) : null;
        canvasPainting.Duration = namedQueryCanvas.Duration;
        canvasPainting.Ingesting = false;
        canvasPainting.Modified = DateTime.UtcNow;

        if (canvasPainting is { StaticWidth: null, StaticHeight: null })
        {
            // #232: set static width/height if not provided.
            // if canvas painting comes with statics, use them
            // otherwise, if we can find dimensions within, use those
            // ReSharper disable once InvertIf
            if (namedQueryCanvas.GetCanvasDimensions() is var (width, height))
            {
                canvasPainting.StaticWidth = width;
                canvasPainting.StaticHeight = height;
            }
        }
        else if (canvasPainting is { StaticWidth: { } staticWidth, StaticHeight: { } staticHeight }
                 && body is Image image)
        {
            // #232: if static_width/height provided in canvasPainting
            // then don't use ones from NQ
            // and set the body to those dimensions + resized id (image request uri)
            logger.LogTrace("CanvasPainting {Id} has static width and height, updating body",
                canvasPainting.CanvasPaintingId);
            image.Width = staticWidth;
            image.Height = staticHeight;
            image.Id = pathGenerator.GetModifiedImageRequest(image.Id, staticWidth, staticHeight);
        }
    }
    
    private void SetManifestContext(Manifest baseManifest, Manifest namedQueryManifest)
    {
        // Grab any contexts from NQ manifest
        IEnumerable<string> contexts = namedQueryManifest.Context switch
        {
            null => [],
            string str => [str],
            IEnumerable<string> enumerable => enumerable,
            JArray jArray => jArray.Values<string>(),
            JValue { Type: JTokenType.String } jValue when jValue.ToString() is { } plain => [plain],
            _ => []
        };
        
        // skip the default one
        contexts = contexts.Where(c => !Context.Presentation3Context.Equals(c));

        // ensure if any
        foreach (var context in contexts)
        {
            logger.LogTrace("Adding context {Context} to {ManifestId}", context, baseManifest.Id);
            baseManifest.EnsureContext(context);
        }
    }

    /// <summary>
    /// Get first <see cref="PaintingAnnotation"/> from canvas, throwing if not found
    /// </summary>
    private static PaintingAnnotation GetSafeFirstPaintingAnnotation(Canvas canvas) =>
        (canvas.Items?[0].Items?[0] as PaintingAnnotation).ThrowIfNull("PaintingAnnotation");
    
    /// <summary>
    /// Get first <see cref="IPaintable"/> from canvas, throwing if not found.
    /// If <see cref="IPaintable"/> is <see cref="Image"/> the returned object will be a clone as we may update some
    /// properties on it (id and dimensions) if static_width and static_height have been set. Cloning will avoid
    /// any issues
    /// </summary>
    private static IPaintable GetSafeFirstPaintingAnnotationBody(Canvas canvas)
    {
        var paintable = GetSafeFirstPaintingAnnotation(canvas).Body.ThrowIfNull("PaintingAnnotation.Body");
        return paintable is Image image
            ? new Image
            {
                Id = image.Id,
                Format = image.Format,
                Width = image.Width,
                Height = image.Height,
                Service = image.Service,
            }
            : paintable;
    }
}
