using Models.Database.Collections;

namespace API.Features.Storage.Models;

public class CollectionWithItems
{
    public CollectionWithItems(Collection? collection, List<Collection>? items, int totalItems)
    {
        Collection = collection;
        Items = items;
        TotalItems = totalItems;
    }

    public Collection? Collection { get; init; }
    public List<Collection>? Items { get; init; }
    public int TotalItems { get; init; }

    public void Deconstruct(out Collection? collection, out List<Collection>? items, out int totalItems)
    {
        collection = Collection;
        items = Items;
        totalItems = TotalItems;
    }
}