using API.Features.Manifest.Exceptions;
using Core.Helpers;
using IIIF.Presentation.V3;
using Repository.Paths;
using Services.Manifests.Model;

namespace API.Features.Manifest;

public interface ICanvasPaintingMerger
{
    public List<InterimCanvasPainting>? CombinePaintedResources(List<InterimCanvasPainting> itemsCanvasPaintings,
        List<InterimCanvasPainting> paintedResourceCanvasPaintings, List<Canvas>? items);
}

/// <summary>
/// Merges canvas painting objects from items and painted resources
/// </summary>
public class CanvasPaintingMerger(IPathRewriteParser pathRewriteParser) : ICanvasPaintingMerger
{
    public List<InterimCanvasPainting> CombinePaintedResources(List<InterimCanvasPainting> itemsCanvasPaintings, 
        List<InterimCanvasPainting> paintedResourceCanvasPaintings, List<Canvas>? items)
    {
        var combinedCanvasPaintings = new List<InterimCanvasPainting>();
        
        JoinPaintedResourcesWithItems(itemsCanvasPaintings, paintedResourceCanvasPaintings, items);
        
        CheckForDuplicates(itemsCanvasPaintings, paintedResourceCanvasPaintings);
        
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
                    
                    AddCanvasPaintingsFromItems(groupedItemsCanvasPaintings, canvasPaintingsToAdd,
                        canvasPaintingOrderTracker, combinedCanvasPaintings);
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
    
    private static void CheckForDuplicates(List<InterimCanvasPainting> itemsCanvasPaintings, List<InterimCanvasPainting> paintedResourceCanvasPaintings)
    {
        var matchedCanvasPainting =
            paintedResourceCanvasPaintings.Where(p => itemsCanvasPaintings.Any(i => p.CanvasOrder == i.CanvasOrder))
                .ToList();
        
        if (matchedCanvasPainting.Count != 0)
        {
            throw new CanvasPaintingMergerException(
                "The following canvas painting records conflict with the order from items - " +
                $"{string.Join(',', 
                    matchedCanvasPainting.Select(m => $"({(m.Id != null ? $"id: {m.Id}, " : "")}canvasOrder: {m.CanvasOrder})"))}");
        }
    }
    
    private void JoinPaintedResourcesWithItems(List<InterimCanvasPainting> itemsCanvasPaintings, 
        List<InterimCanvasPainting> paintedResourceCanvasPaintings, List<Canvas>? items)
    {
        // this avoids issues with joined composite canvas paintings, by making sure the order correctly matches
        var currentCanvasOrder = 0;
        foreach (var itemsCanvasPainting in itemsCanvasPaintings.ToList())
        {
            var matchedPaintedResourceCanvasPaintings = paintedResourceCanvasPaintings.Where(cp =>
                !string.IsNullOrEmpty(cp.Id) && string.Equals(cp.Id, itemsCanvasPainting.Id,
                    StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchedPaintedResourceCanvasPaintings.Count == 0) continue;
            
            CheckForMismatchedCase(matchedPaintedResourceCanvasPaintings, itemsCanvasPainting);
            
            // we check by canvas/choice order as well, in case there are multiple canvases with the same id (possible with placeholders etc.)
            InterimCanvasPainting? orderedCanvasPainting;

            if (matchedPaintedResourceCanvasPaintings.Count == 1)
            {
                orderedCanvasPainting = matchedPaintedResourceCanvasPaintings.Single();
            }
            else
            {
                orderedCanvasPainting =
                    matchedPaintedResourceCanvasPaintings.FirstOrDefault(cp =>
                        cp.CanvasOrder == itemsCanvasPainting.CanvasOrder &&
                        cp.ChoiceOrder == itemsCanvasPainting.ChoiceOrder) ?? throw new CanvasPaintingMergerException(
                        $"Canvas with id {itemsCanvasPainting.CanvasOriginalId} refers to multiple canvases, and the matching canvas order cannot be found");
            }
            
            ValidateItemCanvasPainting(itemsCanvasPainting, orderedCanvasPainting, items, currentCanvasOrder);

            matchedPaintedResourceCanvasPaintings.ForEach(c => c.CanvasPaintingType = CanvasPaintingType.Mixed);

            MatchImplicitItemsToCanvasOrder(itemsCanvasPainting, matchedPaintedResourceCanvasPaintings);

            itemsCanvasPaintings.Remove(itemsCanvasPainting);
            currentCanvasOrder += matchedPaintedResourceCanvasPaintings.Count;
        }
    }

    private void CheckForMismatchedCase(List<InterimCanvasPainting> matchedPaintedResourceCanvasPaintings, InterimCanvasPainting itemsCanvasPainting)
    {
        var distinctCanvasPaintings =
            matchedPaintedResourceCanvasPaintings.Select(cp => cp.Id).Distinct().ToList();

        if (distinctCanvasPaintings.Count > 1 || itemsCanvasPainting.Id != distinctCanvasPaintings.SingleOrDefault())
        {
            throw new CanvasPaintingMergerException(
                $"Canvas painting from items with id {itemsCanvasPainting.Id} has a mismatched case with painted resource(s) {string.Join(',', distinctCanvasPaintings)}");
        }
    }

    private static void MatchImplicitItemsToCanvasOrder(InterimCanvasPainting itemsCanvasPainting,
        List<InterimCanvasPainting> matchedPaintedResourceCanvasPaintings)
    {
        var currentItemCanvasOrder = itemsCanvasPainting.CanvasOrder;

        foreach (var matchedPaintedResourceCanvasPainting in matchedPaintedResourceCanvasPaintings)
        {
            if (matchedPaintedResourceCanvasPainting.CanvasOrder != currentItemCanvasOrder && matchedPaintedResourceCanvasPainting.ImplicitOrder)
            {
                matchedPaintedResourceCanvasPainting.CanvasOrder = currentItemCanvasOrder;
            }

            currentItemCanvasOrder++;
        }
    }

    private void ValidateItemCanvasPainting(InterimCanvasPainting itemsCanvasPainting,
        InterimCanvasPainting paintedResourceCanvasPainting,
        List<Canvas>? items, int currentCanvasOrder)
    {
        var canvas = items?.FirstOrDefault(c =>
            pathRewriteParser.ParsePathWithRewrites(c.Id, itemsCanvasPainting.CustomerId).Resource ==
            itemsCanvasPainting.Id);

        if (canvas != null)
        {
            if (!canvas.Items.IsNullOrEmpty())
            {
                throw new CanvasPaintingMergerException(
                    $"Canvas painting with id {itemsCanvasPainting.Id} cannot contain an annotation body");
            }
        }

        if (paintedResourceCanvasPainting.CanvasLabel == null && itemsCanvasPainting.CanvasLabel != null)
        {
            paintedResourceCanvasPainting.CanvasLabel = itemsCanvasPainting.CanvasLabel;
        }
        
        
        if (itemsCanvasPainting.CanvasLabel != paintedResourceCanvasPainting.CanvasLabel)
        {
            throw new CanvasPaintingMergerException(paintedResourceCanvasPainting.CanvasLabel?.ToString(),
                itemsCanvasPainting.CanvasLabel?.ToString(),
                paintedResourceCanvasPainting.Id,
                $"Canvas painting with id {paintedResourceCanvasPainting.Id} does not have a matching canvas label");
        }
        
        
        if (itemsCanvasPainting.Label != null || paintedResourceCanvasPainting.Label != null)
        {
            if (itemsCanvasPainting.Label?.Any(entry =>
                {
                    List<string>? languageMapValues = null;
                    paintedResourceCanvasPainting.Label?.TryGetValue(entry.Key, out languageMapValues);
                    if (languageMapValues == null) return true;
                    
                    return !languageMapValues.SequenceEqual(entry.Value);
                }) ?? true)
            {
                throw new CanvasPaintingMergerException(paintedResourceCanvasPainting.Label?.ToString(),
                    itemsCanvasPainting.Label?.ToString(),
                    paintedResourceCanvasPainting.Id,
                    $"Canvas painting with id {paintedResourceCanvasPainting.Id} does not have a matching label");
            }
        }

        if (currentCanvasOrder != paintedResourceCanvasPainting.CanvasOrder)
        {
            throw new CanvasPaintingMergerException(itemsCanvasPainting.CanvasOrder.ToString(),
                paintedResourceCanvasPainting.CanvasOrder.ToString(),
                paintedResourceCanvasPainting.Id,
                $"Canvas painting with id {paintedResourceCanvasPainting.Id} does not have a matching canvas order");
        }
        
        if (itemsCanvasPainting.ChoiceOrder != paintedResourceCanvasPainting.ChoiceOrder)
        {
            throw new CanvasPaintingMergerException(paintedResourceCanvasPainting.ChoiceOrder.ToString(),
                itemsCanvasPainting.ChoiceOrder.ToString(),
                paintedResourceCanvasPainting.Id,
                $"Canvas painting with id {paintedResourceCanvasPainting.Id} does not have a matching choice order");
        }
    }

    private static void AddCanvasPaintingsFromItems(
        List<IGrouping<int, InterimCanvasPainting>> groupedItemsCanvasPaintings, int canvasPaintingsToAdd,
        int canvasPaintingOrderTracker, List<InterimCanvasPainting> combinedCanvasPaintings)
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
