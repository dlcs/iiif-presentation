using Models.Database.Collections;

namespace API.Features.Storage.Models;

public class CollectionWithItems(
    Collection? collection,
    List<Collection>? items,
    int totalItems,
    string? storedCollection = null)
{
    public Collection? Collection { get; init; } = collection;
    public List<Collection>? Items { get; init; } = items;
    public int TotalItems { get; init; } = totalItems;
    public string? StoredCollection { get; init; } = storedCollection;

    public void Deconstruct(out Collection? collection, out List<Collection>? items, out int totalItems, 
        out string? storedCollection)
    {
        collection = Collection;
        items = Items;
        totalItems = TotalItems;
        storedCollection = StoredCollection;
    }
}