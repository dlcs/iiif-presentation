using Models.Database.Collections;

namespace API.Features.Storage.Models;

public class CollectionWithItems
{
    public CollectionWithItems(Collection? collection, IQueryable<Collection>? items)
    {
        Collection = collection;
        Items = items;
    }

    public Collection? Collection { get; init; }
    public IQueryable<Collection>? Items { get; init; }

    public void Deconstruct(out Collection? collection, out IQueryable<Collection>? items)
    {
        collection = Collection;
        items = Items;
    }
}