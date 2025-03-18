using System.Diagnostics;
using Core.Helpers;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Models.DLCS;
using CanvasPainting = Models.Database.CanvasPainting;

namespace BackgroundHandler.Helpers;

public static class ManifestMerger 
{
    /// <summary>
    /// Merges a generated DLCS manifest with the current manifest in S3
    /// </summary>
    public static Manifest Merge(Manifest baseManifest, List<CanvasPainting>? canvasPaintings, 
        Dictionary<AssetId, Canvas> canvasDictionary)
    {
        // Ensure collection non-null
        baseManifest.Items ??= [];

        var indexBasedManifest = baseManifest.Items.Select((canvas, index) => (Canvas: canvas, Index: index)).ToList();
        // get everything in the right order, then group by so we can tell where one choice ends and the next begins
        var orderedCanvasPaintings = canvasPaintings?.OrderBy(cp => cp.CanvasOrder).ThenBy(cp => cp.ChoiceOrder)
            .GroupBy(cp => cp.CanvasOrder).ToList() ?? [];
        
        // We want to use the canvas order set from painted resource, rather than the order set from the named query
        foreach (var groupedCanvasPaintings in
                 orderedCanvasPaintings.Select((item, index) => (Item: item, Index: index)))
        {
            _ = groupedCanvasPaintings.Item.TryGetNonEnumeratedCount(out var totalGroupedItems);
            foreach (var canvasPainting in groupedCanvasPaintings.Item)
            {
                if (!canvasDictionary.TryGetValue(canvasPainting.AssetId!, out var namedQueryItem)) continue;

                var existingCanvas = indexBasedManifest.FirstOrDefault(bm => bm.Canvas.Id == namedQueryItem.Id);

                // remove canvas metadata as it's not required
                namedQueryItem.Metadata = null;

                if (totalGroupedItems == 1)
                {
                    AddOrUpdateIndividualCanvas(baseManifest, existingCanvas, namedQueryItem, canvasPainting);
                }
                else
                {
                    if (existingCanvas.Canvas?.Id != null)
                    {
                        UpdateChoice(baseManifest, groupedCanvasPaintings.Index, namedQueryItem, canvasPainting);
                    }
                    else
                    {
                        AddChoice(baseManifest, groupedCanvasPaintings.Index, namedQueryItem, canvasPainting);
                    }
                }
            }
        }
        
        return baseManifest;
    }

    private static void UpdateChoice(Manifest baseManifest, int index, Canvas namedQueryCanvas, 
        CanvasPainting canvasPainting)
    {
        // grab the body of the selected resource and base manifest to update
        var baseManifestPaintingAnnotations = baseManifest.Items![index].Items!;
        var paintingAnnotation = baseManifestPaintingAnnotations.GetFirstPaintingAnnotation();
        var basePaintingChoice = paintingAnnotation!.Body as PaintingChoice;
        
        var namedQueryAnnotation = namedQueryCanvas.GetFirstPaintingAnnotation();

        var basePaintingChoiceIndex = basePaintingChoice?.Items?.OfType<ResourceBase>().ToList()
            .FindIndex(pa => pa.Id == namedQueryCanvas.Id);

        var namedQueryBodyObj = namedQueryAnnotation!.Body.ThrowIfNull("namedQueryAnnotation.Body");

        if (namedQueryBodyObj is ResourceBase namedQueryResource)
            namedQueryResource.Label = canvasPainting.Label;

        // this is moving a resource to a choice order
        if (basePaintingChoiceIndex is -1 or null)
        {
            // this is replacing the existing resource in items with a painting choice
            baseManifest.Items = 
            [
                new Canvas
                {
                    Items =
                    [
                        new AnnotationPage
                        {
                            Id = namedQueryCanvas.GetFirstAnnotationPage()?.Id,
                            Label = namedQueryCanvas.GetFirstAnnotationPage()?.Label,
                            Items = []
                        }
                    ],
                    Width = namedQueryCanvas.Width,
                    Height = namedQueryCanvas.Height,
                    Duration = namedQueryCanvas.Duration,
                    Behavior = namedQueryCanvas.Behavior,
                    Rendering = namedQueryCanvas.Rendering,
                    Thumbnail = namedQueryCanvas.Thumbnail
                }
            ];

            // If NQ body is choice, use it - otherwise create new
            // and init with the non-choice IPaintable as sole item
            basePaintingChoice = namedQueryBodyObj as PaintingChoice
                                 ?? new()
                                 {
                                     Items = [namedQueryBodyObj]
                                 };
            
            // update the identifier and label of the canvas based on the identifier and label of the first choice
            baseManifest.Items[index].Id = namedQueryCanvas.Id;
            baseManifest.Items[index].Label = SetCanvasLabel(canvasPainting);

            paintingAnnotation.Body = basePaintingChoice;
            paintingAnnotation.Id ??= namedQueryAnnotation.Id;
            paintingAnnotation.Label = null;
            paintingAnnotation.Target ??=  new Canvas { Id = baseManifest.Items![index].Id };
            baseManifest.GetCurrentCanvasAnnotationPage(index).Items!.Add(paintingAnnotation);
        }
        else
        {
            // Implied by basePaintingChoiceIndex having a >-1 value
            Debug.Assert(basePaintingChoice?.Items is not null, "basePaintingChoice?.Items is not null");

            // update the manifest with the updated resource(s)
            if (namedQueryBodyObj is PaintingChoice {Items: {Count: > 0} namedQueryChoiceItems})
                basePaintingChoice.Items = CombineChoices(basePaintingChoice.Items, namedQueryChoiceItems,
                    basePaintingChoiceIndex.Value);
            else
                basePaintingChoice.Items![basePaintingChoiceIndex.Value] = namedQueryBodyObj;

            basePaintingChoice.Service ??= namedQueryAnnotation.Service;
            paintingAnnotation.Body = basePaintingChoice;
            baseManifest.GetCurrentCanvasAnnotationPage(index).Items![0] = paintingAnnotation;
            var indexedItem = baseManifest.Items[index];
            indexedItem.Duration = namedQueryCanvas.Duration;
            indexedItem.Behavior = namedQueryCanvas.Behavior;
            indexedItem.Rendering =
                CombineRendering(indexedItem.Rendering, namedQueryCanvas.Rendering);
            indexedItem.Thumbnail = namedQueryCanvas.Thumbnail;
        }
    }

    private static List<ExternalResource>? CombineRendering(List<ExternalResource>? existing,
        List<ExternalResource>? incoming)
    {
        if (existing is null) return incoming;
        return incoming is null ? null : existing.Concat(incoming).ToList();
    }

    /// <summary>
    ///     Replaces <paramref name="items" /> item at index <paramref name="index" />
    ///     with the items within provided <paramref name="choice" />
    /// </summary>
    /// <param name="items">Existing <see cref="PaintingChoice.Items" /></param>
    /// <param name="choice">
    ///     NQ-supplied <see cref="PaintingChoice.Items" /> as update to an item at <paramref name="index" />
    /// </param>
    /// <param name="index">index of the element of <paramref name="items" /> to be updated(replaced with contents)</param>
    /// <returns></returns>
    private static List<IPaintable> CombineChoices(List<IPaintable> items, List<IPaintable> choice, int index)
        => [..items[..index], ..choice, ..items[(index + 1)..]];

    private static void AddChoice(Manifest baseManifest, int index,
        Canvas namedQueryCanvas, CanvasPainting canvasPainting)
    {
        List<AnnotationPage> baseManifestPaintingAnnotations;
        var namedQueryAnnotationPage = namedQueryCanvas.GetFirstAnnotationPage();
        var namedQueryAnnotation = namedQueryAnnotationPage?.GetFirstPaintingAnnotation();
        
        // is this the first item in a new choice order?
        if (baseManifest.Items?.Count == index)
        {
            // if it is, set up the first annotation page with objects and then populate
            baseManifest.Items ??= [];
            baseManifestPaintingAnnotations = CreateInitialAnnotationPageList(namedQueryAnnotationPage);

            baseManifest.Items.Add(new()
            {
                Items = baseManifestPaintingAnnotations,
                Height = namedQueryCanvas.Height,
                Width = namedQueryCanvas.Width,
                Duration = namedQueryCanvas.Duration,
                Behavior = namedQueryCanvas.Behavior,
                Rendering = namedQueryCanvas.Rendering,
                Thumbnail = namedQueryCanvas.Thumbnail
            });
            
            // set the identifier and label of the canvas based on the identifier and label of the first choice
            baseManifest.Items[index].Id ??= namedQueryCanvas.Id;
            baseManifest.Items[index].Label ??= SetCanvasLabel(canvasPainting);
        }
        else
        {
            // otherwise, just use the already created choice order
           baseManifestPaintingAnnotations = baseManifest.Items![index].Items!;
        }
        
        var paintingAnnotation = baseManifestPaintingAnnotations.GetFirstPaintingAnnotation();


        // use the labels from the first choice as the annotation
        paintingAnnotation!.Id ??= namedQueryAnnotation?.Id;
        paintingAnnotation.Label = null;
        paintingAnnotation.Target ??=  new Canvas { Id = baseManifest.Items![index].Id };

        // convert the namedQuery manifest resource into a painting choice on the base manifest
        // if no defaults are set for the base resource, set them (this happens on the first choice order)
        var basePaintingChoice = paintingAnnotation.Body as PaintingChoice
                                 ?? new()
                                 {
                                     Items = []
                                 };

        var namedQueryBodyObj = namedQueryAnnotation!.Body.ThrowIfNull("namedQueryAnnotation.Body");

        if (namedQueryBodyObj is ResourceBase namedQueryResource)
            namedQueryResource.Label = canvasPainting.Label;

        // add the new resource(s) to the base resource choice, then update the manifest with the changes
        if (namedQueryBodyObj is PaintingChoice {Items: {Count: > 0} namedQueryChoiceItems})
            foreach (var nqItem in namedQueryChoiceItems)
                basePaintingChoice.Items!.Add(nqItem);
        else
            basePaintingChoice.Items!.Add(namedQueryBodyObj);


        basePaintingChoice.Service ??= namedQueryAnnotation.Service;
        paintingAnnotation.Body = basePaintingChoice;
        baseManifest.GetCurrentCanvasAnnotationPage(index).Items![0] = paintingAnnotation;
    }

    private static List<AnnotationPage> CreateInitialAnnotationPageList(AnnotationPage? annotationPage)
    {
        return 
        [
            new AnnotationPage
            {
                Id = annotationPage?.Id,
                Label = annotationPage?.Label,
                Items = [new PaintingAnnotation()]
            }
        ];
    }

    private static void AddOrUpdateIndividualCanvas(Manifest baseManifest, (Canvas? Canvas, int Index) existingCanvas,
        Canvas namedQueryCanvas, CanvasPainting canvasPainting)
    {
        // set the label to the correct canvas label
        namedQueryCanvas.Label = SetCanvasLabel(canvasPainting);

        if (existingCanvas.Canvas != null)
        {
            baseManifest.Items![existingCanvas.Index] = namedQueryCanvas;
        }
        else
        {
            baseManifest.Items!.Add(namedQueryCanvas);
        }
    }

    private static LanguageMap? SetCanvasLabel(CanvasPainting canvasPainting)
        => canvasPainting.CanvasLabel ?? canvasPainting.Label;
}
