using Core.Helpers;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Models.DLCS;
using CanvasPainting = Models.Database.CanvasPainting;

namespace BackgroundHandler.Helpers;

public static class ManifestMerger
{
    /// <summary>
    /// Merges a generated DLCS manifest with the current manifest in S3
    /// </summary>
    public static Manifest Merge(Manifest baseManifest, List<CanvasPainting>? canvasPaintings, 
        Dictionary<AssetId, Canvas> itemDictionary, List<ExternalResource>? thumbnail)
    {
        if (baseManifest.Items == null) baseManifest.Items = [];

        var indexBasedManifest = baseManifest.Items.Select((item, index) => (item, index)).ToList();
        // get everything in the right order, then group by so we can tell where one choice ends and the next begins
        var orderedCanvasPaintings = canvasPaintings?.OrderBy(cp => cp.CanvasOrder).ThenBy(cp => cp.ChoiceOrder).GroupBy(cp => cp.CanvasOrder).ToList() ?? [];
        
        // We want to use the canvas order set when creating assets, rather than the 
        foreach (var groupedCanvasPaintings in orderedCanvasPaintings.Select((item, index) => (item, index)))
        {
            groupedCanvasPaintings.item.TryGetNonEnumeratedCount(out var totalGroupedItems);
            foreach (var canvasPainting in groupedCanvasPaintings.item)
            {
                if (!itemDictionary.TryGetValue(canvasPainting.AssetId!, out var namedQueryItem)) continue;

                var existingItem = indexBasedManifest.FirstOrDefault(bm => bm.item.Id == namedQueryItem.Id);

                // remove canvas metadata as it's not required
                namedQueryItem.Metadata = null;

                if (totalGroupedItems == 1)
                {
                    AddOrUpdateIndividualCanvas(baseManifest, existingItem, namedQueryItem);
                }
                else
                {
                    if (existingItem.item?.Id != null)
                    {
                        UpdateChoiceOrder(baseManifest, groupedCanvasPaintings.index, namedQueryItem);
                    }
                    else
                    {
                        AddChoiceOrder(baseManifest, groupedCanvasPaintings.index, namedQueryItem);
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

    private static void UpdateChoiceOrder(Manifest baseManifest, int index, Canvas namedQueryItem)
    {
        // grab the body of the selected image and base maniest to update
        var baseManifestPaintingAnnotations = baseManifest.Items![index].Items!;
        var paintingAnnotation = baseManifestPaintingAnnotations.GetFirstPaintingAnnotation();
        var baseImage = paintingAnnotation!.Body as PaintingChoice; 
        var namedQueryAnnotation = namedQueryItem.GetFirstPaintingAnnotation();
        var namedQueryImage = (Image)namedQueryAnnotation!.Body!;
        
        var baseImageIndex = baseImage?.Items?.OfType<Image>().ToList()
            .FindIndex(pa => pa.Id == namedQueryItem.Id);
        
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
                        Id = namedQueryItem.GetFirstAnnotationPage()?.Id,
                        Label = namedQueryItem.GetFirstAnnotationPage()?.Label,
                        Items = []
                    }],
                    Width = namedQueryItem.Width,
                    Height = namedQueryItem.Height,
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
            baseManifest.Items[index].Id = namedQueryItem.Id;
            baseManifest.Items[index].Label = namedQueryItem.Label;
            
            paintingAnnotation.Body = baseImage;
            paintingAnnotation.Id ??= namedQueryAnnotation.Id;
            paintingAnnotation.Label ??= namedQueryAnnotation.Label;
            paintingAnnotation.Target ??=  new Canvas { Id = baseManifest.Items![index].Id };
            baseManifest.GetCurrentCanvasAnnotation(index).Items!.Add(paintingAnnotation);
        }
        else
        {
            // update the manifest with the updated image
            baseImage.Items![baseImageIndex.Value] = namedQueryImage;
            baseImage.Service ??= namedQueryAnnotation.Service;
            paintingAnnotation.Body = baseImage;
            baseManifest.GetCurrentCanvasAnnotation(index).Items![0] = paintingAnnotation;
        }
    }

    private static void AddChoiceOrder(Manifest baseManifest, int index,
        Canvas namedQueryItem)
    {
        List<AnnotationPage> baseManifestPaintingAnnotations;
        var namedQueryAnnotationPage = namedQueryItem.GetFirstAnnotationPage();
        var namedQueryAnnotation = namedQueryAnnotationPage?.GetFirstPaintingAnnotation();
        var namedQueryImage = (Image)namedQueryAnnotation?.Body;
        
        // is this the first item in a new choice order?
        if (baseManifest.Items?.Count == index)
        {
            // if it is, set up the first annotation page with objects and then populate, so errors aren't thrown
            baseManifest.Items ??= [];
            baseManifestPaintingAnnotations = CreateEmptyPaintingAnnotations(namedQueryAnnotationPage);
            
            baseManifest.Items.Add(new Canvas
            {
                Items = baseManifestPaintingAnnotations,
                Height = namedQueryImage?.Height,
                Width = namedQueryImage?.Width,
            });
            
            // set the identifier and label of the canvas based on the identifier and label of the first choice
            baseManifest.Items[index].Id ??= namedQueryItem.Id;
            baseManifest.Items[index].Label ??= namedQueryItem.Label;
        }
        else
        {
            // otherwise, just use the already created choice order
           baseManifestPaintingAnnotations = baseManifest.Items![index].Items!;
        }
        
        // grab the painting annotations from both items
        var paintingAnnotation = baseManifestPaintingAnnotations.GetFirstPaintingAnnotation();
        
        // convert the namedQuery manifest image into a painting choice on the base manifest
        var baseImage = paintingAnnotation!.Body as PaintingChoice;
        // use the labels from the first choice as the annotation
        paintingAnnotation.Id ??= namedQueryAnnotation?.Id;
        paintingAnnotation.Label ??= namedQueryAnnotation?.Label;
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
        baseManifest.GetCurrentCanvasAnnotation(index).Items![0] = paintingAnnotation;
    }

    private static List<AnnotationPage> CreateEmptyPaintingAnnotations(AnnotationPage? paintingAnnotation)
    {
        List<AnnotationPage> baseManifestPaintingAnnotations;
        baseManifestPaintingAnnotations =
        [
            new AnnotationPage
            {
                Id = paintingAnnotation?.Id,
                Label = paintingAnnotation?.Label,
                Items = [new PaintingAnnotation()]
            }
        ];
        return baseManifestPaintingAnnotations;
    }

    private static void AddOrUpdateIndividualCanvas(Manifest baseManifest, (Canvas? item, int index) existingItem,
        Canvas namedQueryItem)
    {
        if (existingItem.item != null)
        {
            baseManifest.Items![existingItem.index] = namedQueryItem;
        }
        else
        {
            baseManifest.Items!.Add(namedQueryItem);
        }
    }
}
