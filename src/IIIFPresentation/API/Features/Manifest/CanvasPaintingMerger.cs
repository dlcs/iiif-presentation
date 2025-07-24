using Models.Database;

namespace API.Features.Manifest;

public interface ICanvasPaintingMerger
{
    public List<CanvasPainting>? CombinePaintedResources(List<CanvasPainting> itemsCanvasPaintings,
        List<CanvasPainting> paintedResourceCanvasPaintings);
}

/// <summary>
/// Merges canvas painting objects from items and painted resources
/// </summary>
public class CanvasPaintingMerger : ICanvasPaintingMerger
{
    public List<CanvasPainting>? CombinePaintedResources(List<CanvasPainting> itemsCanvasPaintings, 
        List<CanvasPainting> paintedResourceCanvasPaintings)
    {
        var combinedCanvasPaintings = new List<CanvasPainting>();
        
        //todo: first step - work out joins between items and painted resources using canvas original id
        
        var groupedItemsCanvasPaintings = itemsCanvasPaintings.GroupBy(cp => cp.CanvasOrder).ToList();

        var canvasPaintingOrderTracker = 0;

        foreach (var paintedResourceCanvasPainting in paintedResourceCanvasPaintings.OrderBy(cp => cp.CanvasOrder))
        {
            // current canvas we're checking matches the order from PR - so add straight away
            if (paintedResourceCanvasPainting.CanvasOrder == canvasPaintingOrderTracker)
            {
                combinedCanvasPaintings.Add(paintedResourceCanvasPainting);
            }
            else
            {
                // we have canvas paintings from items, so add until we match the next PR canvas order
                if (groupedItemsCanvasPaintings.Count != 0)
                {
                    var canvasPaintingsToAdd = paintedResourceCanvasPainting.CanvasOrder - canvasPaintingOrderTracker;

                    AddCanvasPaintingsFromItems(groupedItemsCanvasPaintings, canvasPaintingsToAdd, canvasPaintingOrderTracker, combinedCanvasPaintings);
                    
                    groupedItemsCanvasPaintings.RemoveRange(0, canvasPaintingsToAdd);
                }
                // no more canvas paintings from items - so everything after this is from PR
                else
                {
                    // todo: add range and break instead?
                    combinedCanvasPaintings.Add(paintedResourceCanvasPainting);
                }
            }

            canvasPaintingOrderTracker = paintedResourceCanvasPainting.CanvasOrder;
        }

        // Add everything that's left over in the items canvas painting, if needed
        if (groupedItemsCanvasPaintings.Count != 0)
        {
            AddCanvasPaintingsFromItems(groupedItemsCanvasPaintings, groupedItemsCanvasPaintings.Count,
                canvasPaintingOrderTracker, combinedCanvasPaintings);
        }
        
        return combinedCanvasPaintings;
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
    }
}
