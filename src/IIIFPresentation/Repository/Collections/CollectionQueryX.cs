using Core.Helpers;
using Models.Database.General;

namespace Repository.Collections;

public static class CollectionQueryX
{
    /// <summary>
    /// Optionally adds ordering statements to the collection IQueryable
    /// </summary>
    public static IQueryable<Hierarchy> AsOrderedCollectionItemsQuery(
        this IQueryable<Hierarchy> hierarchyQuery, 
        string? orderBy,
        bool descending = false)
    {
        if (!orderBy.HasText())
        {
            return OrderByCreated();
        }
        var field = orderBy.ToLowerInvariant();
        return field switch
        {
            "id" => descending 
                ? hierarchyQuery.OrderByDescending(h => h.Manifest == null ? h.Collection.Id : h.Manifest.Id) 
                : hierarchyQuery.OrderBy(h => h.Manifest == null ? h.Collection.Id : h.Manifest.Id),
            "slug" => descending
                ? hierarchyQuery.OrderByDescending(h => h.Slug)
                : hierarchyQuery.OrderBy(h => h.Slug),
            "created" => OrderByCreated(),
            _ => hierarchyQuery
        };

        IOrderedQueryable<Hierarchy> OrderByCreated()
        {
            return descending
                ? hierarchyQuery.OrderByDescending(h => h.Manifest == null ? h.Collection.Created : h.Manifest.Created)
                : hierarchyQuery.OrderBy(h => h.Manifest == null ? h.Collection.Created : h.Manifest.Created);
        }
    }
}