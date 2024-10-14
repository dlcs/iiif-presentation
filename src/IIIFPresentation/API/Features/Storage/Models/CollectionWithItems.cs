using Models.API.Collection;
using Models.Database.Collections;
using Models.Database.General;

namespace API.Features.Storage.Models;

public class CollectionWithItems(
    Collection? collection,
    Hierarchy? hierarchy,
    List<Collection>? items,
    int totalItems,
    string? storedCollection = null)
{
    public Collection? Collection { get; init; } = collection;
    public List<Collection>? Items { get; init; } = items;
    public Hierarchy? Hierarchy { get; init; } = hierarchy;
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