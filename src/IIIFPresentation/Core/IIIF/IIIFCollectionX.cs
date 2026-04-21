using IIIF.Presentation.V3;

namespace Core.IIIF;

public static class IIIFCollectionX
{
    /// <summary>
    /// Add items to a list that are not already present, based in their Id property.
    /// </summary>
    public static void AddDistinctById<T>(this IList<T> target, IEnumerable<T>? source) where T : ResourceBase
    {
        if (source == null) return;

        var existingIds = new HashSet<string?>(target.Select(s => s.Id));
        foreach (var item in source)
        {
            if (!existingIds.Contains(item.Id))
            {
                target.Add(item);
                existingIds.Add(item.Id);
            }
        }
    }
}
