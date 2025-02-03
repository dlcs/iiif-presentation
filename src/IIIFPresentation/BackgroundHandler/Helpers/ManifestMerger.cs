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
        Dictionary<AssetId, Canvas> canvasDictionary, List<ExternalResource>? thumbnail)
    {
        // Ensure collection non-null
        baseManifest.Items ??= [];

        List<(Canvas? canvas, int index)> indexBasedManifest = baseManifest.Items.Select((Canvas, Index) => (Canvas, Index)).ToList();
        // get everything in the right order, then group by so we can tell where one choice ends and the next begins
        var orderedCanvasPaintings = canvasPaintings?.OrderBy(cp => cp.CanvasOrder).ThenBy(cp => cp.ChoiceOrder)
            .GroupBy(cp => cp.CanvasOrder).ToList() ?? [];
        
        // We want to use the canvas order set from painted resource, rather than the order set from the named query
        foreach (var groupedCanvasPaintings in
                 orderedCanvasPaintings.Select((Item, Index) => (Item, Index)))
        {
            _ = groupedCanvasPaintings.Item.TryGetNonEnumeratedCount(out var totalGroupedItems);
            foreach (var canvasPainting in groupedCanvasPaintings.Item)
            {
                if (!canvasDictionary.TryGetValue(canvasPainting.AssetId!, out var namedQueryItem)) continue;

                var existingCanvas = indexBasedManifest.FirstOrDefault(bm => bm.canvas.Id == namedQueryItem.Id);

                // remove canvas metadata as it's not required
                namedQueryItem.Metadata = null;

                if (totalGroupedItems == 1)
                {
                    AddOrUpdateIndividualCanvas(baseManifest, existingCanvas, namedQueryItem, canvasPainting);
                }
                else
                {
                    if (existingCanvas.canvas?.Id != null)
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

        if (baseManifest.Thumbnail.IsNullOrEmpty())
        {
            baseManifest.Thumbnail = thumbnail;
        }
        
        return baseManifest;
    }

    private static void UpdateChoice(Manifest baseManifest, int index, Canvas namedQueryCanvas, 
        CanvasPainting canvasPainting)
    {
        // grab the body of the selected image and base manifest to update
        var baseManifestPaintingAnnotations = baseManifest.Items![index].Items!;
        var paintingAnnotation = baseManifestPaintingAnnotations.GetFirstPaintingAnnotation();
        var baseImage = paintingAnnotation!.Body as PaintingChoice; 
        var namedQueryAnnotation = namedQueryCanvas.GetFirstPaintingAnnotation();
        var namedQueryImage = (Image)namedQueryAnnotation!.Body!;
        
        namedQueryImage.Label = canvasPainting.Label;
        
        var baseImageIndex = baseImage?.Items?.OfType<Image>().ToList()
            .FindIndex(pa => pa.Id == namedQueryCanvas.Id);
        
        // this is moving an image to a choice order
        if (baseImageIndex is -1 or null)
        {
            // this is replacing the existing image in items with a painting choice
            baseManifest.Items = 
            [
                new Canvas
                {
                    Items = [new AnnotationPage
                    {
                        Id = namedQueryCanvas.GetFirstAnnotationPage()?.Id,
                        Label = namedQueryCanvas.GetFirstAnnotationPage()?.Label,
                        Items = []
                    }],
                    Width = namedQueryCanvas.Width,
                    Height = namedQueryCanvas.Height,
                }
            ];

            baseImage = new PaintingChoice
            {
                Items =
                [
                    namedQueryImage
                ]
            };
            
            // update the identifier and label of the canvas based on the identifier and label of the first choice
            baseManifest.Items[index].Id = namedQueryCanvas.Id;
            baseManifest.Items[index].Label = SetCanvasLabel(canvasPainting);
            
            paintingAnnotation.Body = baseImage;
            paintingAnnotation.Id ??= namedQueryAnnotation.Id;
            paintingAnnotation.Label = null;
            paintingAnnotation.Target ??=  new Canvas { Id = baseManifest.Items![index].Id };
            baseManifest.GetCurrentCanvasAnnotationPage(index).Items!.Add(paintingAnnotation);
        }
        else
        {
            // update the manifest with the updated image
            baseImage.Items![baseImageIndex.Value] = namedQueryImage;
            baseImage.Service ??= namedQueryAnnotation.Service;
            paintingAnnotation.Body = baseImage;
            baseManifest.GetCurrentCanvasAnnotationPage(index).Items![0] = paintingAnnotation;
        }
    }

    private static void AddChoice(Manifest baseManifest, int index,
        Canvas namedQueryCanvas, CanvasPainting canvasPainting)
    {
        List<AnnotationPage> baseManifestPaintingAnnotations;
        var namedQueryAnnotationPage = namedQueryCanvas.GetFirstAnnotationPage();
        var namedQueryAnnotation = namedQueryAnnotationPage?.GetFirstPaintingAnnotation();
        
        var namedQueryImage = (Image)namedQueryAnnotation!.Body;
        namedQueryImage.Label = canvasPainting.Label;
        
        // is this the first item in a new choice order?
        if (baseManifest.Items?.Count == index)
        {
            // if it is, set up the first annotation page with objects and then populate, so errors aren't thrown
            baseManifest.Items ??= [];
            baseManifestPaintingAnnotations = CreateInitialAnnotationPageList(namedQueryAnnotationPage);
            
            baseManifest.Items.Add(new Canvas
            {
                Items = baseManifestPaintingAnnotations,
                Height = namedQueryCanvas.Height,
                Width = namedQueryCanvas.Width,
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
        
        // convert the namedQuery manifest image into a painting choice on the base manifest
        var baseImage = paintingAnnotation!.Body as PaintingChoice;
        // use the labels from the first choice as the annotation
        paintingAnnotation.Id ??= namedQueryAnnotation?.Id;
        paintingAnnotation.Label = null;
        paintingAnnotation.Target ??=  new Canvas { Id = baseManifest.Items![index].Id };
        
        // if no defaults are set for the base image, set them (this happens on the first choice order)
        if (baseImage == null)
        {
            baseImage = new PaintingChoice
            {
                Items = []
            };
        }
        
        // add the new image to the base image choice, then update the manifest with the changes
        baseImage.Items!.Add(namedQueryImage);
        baseImage.Service ??= namedQueryAnnotation?.Service;
        paintingAnnotation.Body = baseImage;
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

    private static void AddOrUpdateIndividualCanvas(Manifest baseManifest, (Canvas? canvas, int index) existingCanvas,
        Canvas namedQueryCanvas, CanvasPainting canvasPainting)
    {
        // set the label to the correct canvas label
        namedQueryCanvas.Label = SetCanvasLabel(canvasPainting);

        if (existingCanvas.canvas != null)
        {
            baseManifest.Items![existingCanvas.index] = namedQueryCanvas;
        }
        else
        {
            baseManifest.Items!.Add(namedQueryCanvas);
        }
    }

    private static LanguageMap SetCanvasLabel(CanvasPainting canvasPainting)
    {
        LanguageMap? canvasLabel = null;
        
        if (canvasPainting.CanvasLabel != null)
        {
            canvasLabel = canvasPainting.CanvasLabel;
        }
        else if (canvasPainting.Label != null)
        {
            canvasLabel = canvasPainting.Label;
        }
        
        return canvasLabel;
    }
}
