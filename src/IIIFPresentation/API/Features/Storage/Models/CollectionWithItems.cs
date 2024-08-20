using Models.Database.Collections;

namespace API.Features.Storage.Models;

public class CollectionWithItems
{
    public CollectionWithItems(Collection? Collection, IQueryable<Collection>? Items)
    {
        this.Collection = Collection;
        this.Items = Items;
    }

    public Collection? Collection { get; init; }
    public IQueryable<Collection>? Items { get; init; }

    public void Deconstruct(out Collection? Collection, out IQueryable<Collection>? Items)
    {
        Collection = this.Collection;
        Items = this.Items;
    }
}