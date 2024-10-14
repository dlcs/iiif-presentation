using Core.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Models.Database.Collections;
using Models.Database.General;
using Repository.Helpers;

namespace Repository.Collections;

public static class CollectionQueryX
{
    /// <summary>
    /// Optionally adds ordering statements to the collection IQueryable
    /// </summary>
    /// <param name="collectionQuery"></param>
    /// <param name="orderBy"></param>
    /// <param name="descending"></param>
    /// <returns></returns>
    public static IQueryable<Collection> AsOrderedCollectionQuery(
        this IQueryable<Collection> collectionQuery, 
        string? orderBy,
        bool descending = false)
    {
        if (!orderBy.HasText()) return descending ? collectionQuery.OrderByDescending(c => c.Created)
            : collectionQuery.OrderBy(c => c.Created);
        var field = orderBy.ToLowerInvariant();
        return field switch
        {
            "id" => descending ? collectionQuery.OrderByDescending(c => c.Id) : collectionQuery.OrderBy(c => c.Id),
            "slug" => descending
                ? collectionQuery.OrderByDescending(c => c.Slug)
                : collectionQuery.OrderBy(c => c.Slug),
            "created" => descending
                ? collectionQuery.OrderByDescending(c => c.Created)
                : collectionQuery.OrderBy(c => c.Created),
            _ => collectionQuery
        };
    }
}