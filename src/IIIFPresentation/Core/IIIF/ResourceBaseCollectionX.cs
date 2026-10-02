using IIIF;

namespace Core.IIIF;

public static class ResourceBaseCollectionX
{
    /// <summary>
    /// Add items to a list that are not already present, based on their id property.
    /// </summary>
    /// <param name="target">List to add items to</param>
    /// <param name="source">Items to add</param>
    /// <param name="preAdd">Optional action to run before adding item</param>
    /// <returns>Number of items added</returns>
    public static int AddDistinctById<T>(this IList<T> target, IEnumerable<T>? source, Action<T>? preAdd = null)
        where T : IResource
    {
        if (source == null) return 0;

        var existingIds = new HashSet<string?>(target.Select(s => s.Id));
        int count = 0;
        foreach (var item in source)
        {
            if (!existingIds.Contains(item.Id))
            {
                count++;
                preAdd?.Invoke(item);
                target.Add(item);
                existingIds.Add(item.Id);
            }
        }

        return count;
    }
}
