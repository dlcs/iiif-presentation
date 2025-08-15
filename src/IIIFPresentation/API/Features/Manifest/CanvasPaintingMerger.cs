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

                // add the painted resource after adding everything needed from items to fill out the ordering
                combinedCanvasPaintings.Add(paintedResourceCanvasPainting);
            }

            // set the order tracker to the current canvas order + 1, so that we can handle skipped ordering
            canvasPaintingOrderTracker = paintedResourceCanvasPainting.CanvasOrder;
            canvasPaintingOrderTracker++;
        }

        // Add everything that's left over in the items canvas paintings, if needed
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
        foreach (var itemsCanvasPainting in itemsCanvasPaintings.ToList())
        {
            var paintedResourceCanvasPainting =
                paintedResourceCanvasPaintings.Where(cp => itemsCanvasPainting.CanvasOriginalId != null &&
                    cp.Id == itemsCanvasPainting.Id).ToList();
            
            if (paintedResourceCanvasPainting.Count != 0)
            {
                // we check by canvas/choice order as well, in case there are multiple canvases with the same id (possible with placeholders etc.)
                var orderedCanvasPainting = paintedResourceCanvasPainting.Count == 1
                    ? paintedResourceCanvasPainting.First()
                    : paintedResourceCanvasPainting.FirstOrDefault(cp =>
                        cp.CanvasOrder == itemsCanvasPainting.CanvasOrder &&
                        cp.ChoiceOrder == itemsCanvasPainting.ChoiceOrder) ?? throw new CanvasPaintingMergerException(
                        $"canvas painting with original id {itemsCanvasPainting.CanvasOriginalId} refers to multiple canvases, and the matching canvas order cannot be found");
                
                ValidateItemCanvasPainting(itemsCanvasPainting, orderedCanvasPainting, items!);
                
                itemsCanvasPaintings.Remove(itemsCanvasPainting);
            }
        }
    }

    private void ValidateItemCanvasPainting(CanvasPainting itemsCanvasPainting,
        CanvasPainting paintedResourceCanvasPainting,
        List<Canvas> items)
    {
        var canvas = items.FirstOrDefault(c => c.Id == itemsCanvasPainting.CanvasOriginalId!.ToString());

        if (canvas != null)
        {
            if (!canvas.Items.IsNullOrEmpty())
            {
                throw new CanvasPaintingMergerException(
                    $"canvas painting with original id {itemsCanvasPainting.CanvasOriginalId} cannot contain an annotation body");
            }
        }

        if (paintedResourceCanvasPainting.CanvasLabel == null && itemsCanvasPainting.CanvasLabel != null)
        {
            paintedResourceCanvasPainting.CanvasLabel = itemsCanvasPainting.CanvasLabel;
        }
        
        
        if (itemsCanvasPainting.CanvasLabel != paintedResourceCanvasPainting.CanvasLabel)
        {
            throw new CanvasPaintingMergerException(itemsCanvasPainting.CanvasLabel?.ToString(),
                paintedResourceCanvasPainting.CanvasLabel?.ToString(),
                itemsCanvasPainting.CanvasOriginalId!,
                $"canvas painting with original id {itemsCanvasPainting.CanvasOriginalId} does not have a matching canvas label");
        }
        
        if (itemsCanvasPainting.Label != paintedResourceCanvasPainting.Label)
        {
            throw new CanvasPaintingMergerException(itemsCanvasPainting.Label?.ToString(),
                paintedResourceCanvasPainting.Label?.ToString(),
                itemsCanvasPainting.CanvasOriginalId!,
                $"canvas painting with original id {itemsCanvasPainting.CanvasOriginalId} does not have a matching label");
        }

        if (itemsCanvasPainting.CanvasOrder != paintedResourceCanvasPainting.CanvasOrder)
        {
            throw new CanvasPaintingMergerException(itemsCanvasPainting.CanvasOrder.ToString(),
                itemsCanvasPainting.CanvasOrder.ToString(),
                itemsCanvasPainting.CanvasOriginalId!,
                $"canvas painting with original id {itemsCanvasPainting.CanvasOriginalId} does not have a matching canvas order");
        }
        
        if (itemsCanvasPainting.ChoiceOrder != paintedResourceCanvasPainting.ChoiceOrder)
        {
            throw new CanvasPaintingMergerException(itemsCanvasPainting.CanvasOrder.ToString(),
                itemsCanvasPainting.CanvasOrder.ToString(),
                itemsCanvasPainting.CanvasOriginalId!,
                $"canvas painting with original id {itemsCanvasPainting.CanvasOriginalId} does not have a matching choice order");
        }
    }

    private static void AddCanvasPaintingsFromItems(List<IGrouping<int, CanvasPainting>> groupedItemsCanvasPaintings, int canvasPaintingsToAdd,
        int canvasPaintingOrderTracker, List<CanvasPainting> combinedCanvasPaintings)
    {
        var canvasIdToSet = canvasPaintingOrderTracker;
        
        // avoids out of range exceptions when trying to add more id's than there are
        if (canvasPaintingsToAdd > groupedItemsCanvasPaintings.Count)
        {
            canvasPaintingsToAdd = groupedItemsCanvasPaintings.Count;
        }

        foreach (var grouping in groupedItemsCanvasPaintings.GetRange(0, canvasPaintingsToAdd))
        {
            foreach (var canvasPainting in grouping)
            {
                canvasPainting.CanvasOrder = canvasIdToSet;
            }

            combinedCanvasPaintings.AddRange(grouping);
            canvasIdToSet++;
        }
        
        groupedItemsCanvasPaintings.RemoveRange(0, canvasPaintingsToAdd);
    }
}
