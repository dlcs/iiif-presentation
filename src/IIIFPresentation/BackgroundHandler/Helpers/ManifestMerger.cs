using Core.Helpers;
using IIIF.Presentation.V3;
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

        var indexedBaseManifest = baseManifest.Items.Select((item, index) => (item, index)).ToList();
        var orderedCanvasPaintings = canvasPaintings?.OrderBy(cp => cp.CanvasOrder).ToList() ?? [];
        
        // We want to use the canvas order set when creating assets, rather than the 
        foreach (var canvasPainting in orderedCanvasPaintings)
        {
            itemDictionary.TryGetValue(canvasPainting.AssetId!, out var generatedItem);
            
            if (generatedItem == null) continue;
            
            var existingItem = indexedBaseManifest.FirstOrDefault(bm => bm.item.Id == generatedItem.Id);

            // remove canvas metadata as it's not required
            generatedItem.Metadata = null;
            
            if (existingItem.item != null)
            {
                baseManifest.Items[existingItem.index] = generatedItem;
            }
            else
            {
                baseManifest.Items.Add(generatedItem);
            }
        }

        if (baseManifest.Thumbnail.IsNullOrEmpty())
        {
            baseManifest.Thumbnail = thumbnail;
        }
        
        return baseManifest;
    }
}
