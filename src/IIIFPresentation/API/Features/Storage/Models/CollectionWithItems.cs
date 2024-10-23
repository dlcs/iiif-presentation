using Models.Database.Collections;
using Models.Database.General;

namespace API.Features.Storage.Models;

public class CollectionWithItems(
    Collection? collection,
    List<Hierarchy>? items,
    int totalItems,
    string? storedCollection = null)
{
    public Collection? Collection { get; init; } = collection;
    public List<Hierarchy>? Items { get; init; } = items;
    public int TotalItems { get; init; } = totalItems;
    public string? StoredCollection { get; init; } = storedCollection;

    public static CollectionWithItems Empty { get; private set; } = new(null, null, 0);
}