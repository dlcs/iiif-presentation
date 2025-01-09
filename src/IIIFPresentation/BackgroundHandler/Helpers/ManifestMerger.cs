using Core.Helpers;
using IIIF.Presentation.V3;

namespace BackgroundHandler.Helpers;

public static class ManifestMerger
{
    /// <summary>
    /// Merges a generated DLCS manifest with the current manifest in S3
    /// </summary>
    public static Manifest Merge(Manifest baseManifest, Manifest generatedManifest)
    {
        if (baseManifest.Items == null) baseManifest.Items = new List<Canvas>();

        var indexedBaseManifest = baseManifest.Items.Select((item, index) => (item, index)).ToList();
        
        foreach (var generatedItem in generatedManifest.Items ?? [])
        {
            var existingItem = indexedBaseManifest.FirstOrDefault(bm => bm.item.Id == generatedItem.Id);
            
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
            baseManifest.Thumbnail = generatedManifest.Thumbnail;
        }
        
        return baseManifest;
    }
}
