using API.Features.Manifest.Exceptions;
using Core.Helpers;
using IIIF.Presentation.V3;
using Models.Database;

namespace API.Features.Manifest;

public interface ICanvasPaintingMerger
{
    public List<CanvasPainting>? CombinePaintedResources(List<CanvasPainting> itemsCanvasPaintings,
        List<CanvasPainting> paintedResourceCanvasPaintings, List<Canvas>? items);
}

/// <summary>
/// Merges canvas painting objects from items and painted resources
/// </summary>
public class CanvasPaintingMerger : ICanvasPaintingMerger
{
    // todo: logging
    public List<CanvasPainting>? CombinePaintedResources(List<CanvasPainting> itemsCanvasPaintings, 
        List<CanvasPainting> paintedResourceCanvasPaintings, List<Canvas>? items)
    {
        var combinedCanvasPaintings = new List<CanvasPainting>();
        
        JoinPaintedResourcesWithItems(itemsCanvasPaintings, paintedResourceCanvasPaintings, items);
        
        var groupedItemsCanvasPaintings = itemsCanvasPaintings.GroupBy(cp => cp.CanvasOrder).ToList();

        var canvasPaintingOrderTracker = 0;

        foreach (var paintedResourceCanvasPainting in paintedResourceCanvasPaintings.OrderBy(cp => cp.CanvasOrder))
        {
            // current canvas we're checking matches the order from painted resources - so add straight away
            if (paintedResourceCanvasPainting.CanvasOrder == canvasPaintingOrderTracker)
            {
                combinedCanvasPaintings.Add(paintedResourceCanvasPainting);
            }
            else
            {
                // we have canvas paintings from items, so add until we match the next painted resource canvas order
                if (groupedItemsCanvasPaintings.Count != 0)
                {
                    var canvasPaintingsToAdd = paintedResourceCanvasPainting.CanvasOrder - canvasPaintingOrderTracker;

                    AddCanvasPaintingsFromItems(groupedItemsCanvasPaintings, canvasPaintingsToAdd, canvasPaintingOrderTracker, combinedCanvasPaintings);
                }

                combinedCanvasPaintings.Add(paintedResourceCanvasPainting);
            }

            // set the order tracker to the current canvas order, so that we can handle skipped ordering
            canvasPaintingOrderTracker = paintedResourceCanvasPainting.CanvasOrder;
            canvasPaintingOrderTracker++;
        }

        // Add everything that's left over in the items canvas painting, if needed
        if (groupedItemsCanvasPaintings.Count != 0)
        {
            AddCanvasPaintingsFromItems(groupedItemsCanvasPaintings, groupedItemsCanvasPaintings.Count,
                canvasPaintingOrderTracker, combinedCanvasPaintings);
        }
        
        return combinedCanvasPaintings;
    }

    private void JoinPaintedResourcesWithItems(List<CanvasPainting> itemsCanvasPaintings, 
        List<CanvasPainting> paintedResourceCanvasPaintings, List<Canvas>? items)
    {
        //todo: validate canvasOrder
        
        foreach (var itemsCanvasPainting in itemsCanvasPaintings.ToList())
        {
            var paintedResourceCanvasPainting =
                paintedResourceCanvasPaintings.FirstOrDefault(cp =>
                    cp.CanvasOriginalId == itemsCanvasPainting.CanvasOriginalId);
            
            if (paintedResourceCanvasPainting != null)
            {
                ValidateItemCanvasPainting(itemsCanvasPainting, paintedResourceCanvasPainting, items!);
                
                itemsCanvasPaintings.Remove(itemsCanvasPainting);
            }
        }
    }

    private void ValidateItemCanvasPainting(CanvasPainting itemsCanvasPainting, CanvasPainting paintedResourceCanvasPainting, 
        List<Canvas> items)
    {
        //todo: should we update the canvas painting labels from items if it's null?
        // ANSWER: the one which isn't null wins
        if (itemsCanvasPainting.CanvasLabel != paintedResourceCanvasPainting.CanvasLabel)
        {
            throw new CanvasPaintingMergerException(itemsCanvasPainting.CanvasLabel?.ToString(),
                paintedResourceCanvasPainting.CanvasLabel?.ToString(),
                $"canvas painting with original id {itemsCanvasPainting.CanvasOriginalId} does not have matching canvas label");
        }
        
        if (itemsCanvasPainting.Label != paintedResourceCanvasPainting.Label)
        {
            throw new CanvasPaintingMergerException(itemsCanvasPainting.Label?.ToString(),
                paintedResourceCanvasPainting.Label?.ToString(),
                $"canvas painting with original id {itemsCanvasPainting.CanvasOriginalId} does not have matching label");
        }

        // todo: move to validators?
        if (!items.First(c => c.Id == paintedResourceCanvasPainting.Id).Annotations.IsNullOrEmpty())
        {
            throw new CanvasPaintingMergerException(
                $"canvas painting with original id {itemsCanvasPainting.CanvasOriginalId} cannot contain an annotation body");
        }
    }

    private static void AddCanvasPaintingsFromItems(List<IGrouping<int, CanvasPainting>> groupedItemsCanvasPaintings, int canvasPaintingsToAdd,
        int canvasPaintingOrderTracker, List<CanvasPainting> combinedCanvasPaintings)
    {
        var canvasIdToSet = canvasPaintingOrderTracker;
        
        foreach (var grouping in groupedItemsCanvasPaintings.GetRange(0, canvasPaintingsToAdd))
        {
            canvasIdToSet++;
            
            foreach (var canvasPainting in grouping)
            {
                canvasPainting.CanvasOrder = canvasIdToSet;
            }

            combinedCanvasPaintings.AddRange(grouping);
        }
        
        groupedItemsCanvasPaintings.RemoveRange(0, canvasPaintingsToAdd);
    }
}
