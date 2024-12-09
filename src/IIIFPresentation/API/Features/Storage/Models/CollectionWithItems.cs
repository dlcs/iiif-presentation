using Models.Database.Collections;
using Models.Database.General;

namespace API.Features.Storage.Models;

public class CollectionWithItems(
    Collection? collection,
    List<Hierarchy>? items,
    int totalItems,
    string? storedCollection = null)
{
    public Collection? Collection { get; } = collection;
    public List<Hierarchy>? Items { get; } = items;
    public int TotalItems { get; } = totalItems;
    public string? StoredCollection { get; } = storedCollection;
    public Collection? ParentCollection => Collection?.Hierarchy?.SingleOrDefault()?.ParentCollection;

    /// <summary>
    /// Returns an empty <see cref="CollectionWithItems"/> object
    /// </summary>
    public static CollectionWithItems Empty { get; private set; } = new(null, null, 0);
}
