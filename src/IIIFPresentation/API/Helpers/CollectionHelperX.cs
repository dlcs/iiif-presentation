using System.Data;
using API.Converters;
using API.Infrastructure.IdGenerator;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Models.Database.General;

namespace API.Helpers;

/// <summary>
/// Collection of helpers to generate paths etc. for collections
/// </summary>
public static class CollectionHelperX
{
    private const int MaxAttempts = 3;
    
    public static string GenerateHierarchicalCollectionId(this Collection collection, UrlRoots urlRoots) =>
        $"{urlRoots.BaseUrl}/{collection.CustomerId}{(string.IsNullOrEmpty(collection.FullPath) ? string.Empty : $"/{collection.FullPath}")}";
    
    public static string GenerateFlatCollectionId(this Collection collection, UrlRoots urlRoots) =>
        $"{urlRoots.BaseUrl}/{collection.CustomerId}/collections/{collection.Id}";
    
    public static string GenerateFlatCollectionParent(this Hierarchy hierarchy, UrlRoots urlRoots) =>
        $"{urlRoots.BaseUrl}/{hierarchy.CustomerId}/collections/{hierarchy.Parent}";
    
    public static string GenerateFlatCollectionViewId(this Collection collection, UrlRoots urlRoots, 
        int currentPage, int pageSize, string? orderQueryParam) =>
        $"{collection.GenerateFlatCollectionId(urlRoots)}?page={currentPage}&pageSize={pageSize}{orderQueryParam}";

    public static Uri GenerateFlatCollectionViewNext(this Collection collection, UrlRoots urlRoots,
        int currentPage, int pageSize, string orderQueryParam) =>
        new(
            $"{collection.GenerateFlatCollectionId(urlRoots)}?page={currentPage + 1}&pageSize={pageSize}{orderQueryParam}");
    
    public static Uri GenerateFlatCollectionViewPrevious(this Collection collection, UrlRoots urlRoots, 
        int currentPage, int pageSize, string orderQueryParam) =>
        new(
            $"{collection.GenerateFlatCollectionId(urlRoots)}?page={currentPage - 1}&pageSize={pageSize}{orderQueryParam}");
    
    public static Uri GenerateFlatCollectionViewFirst(this Collection collection, UrlRoots urlRoots, 
        int pageSize, string orderQueryParam) =>
        new(
            $"{collection.GenerateFlatCollectionId(urlRoots)}?page=1&pageSize={pageSize}{orderQueryParam}");
    
    public static Uri GenerateFlatCollectionViewLast(this Collection collection, UrlRoots urlRoots, 
        int lastPage, int pageSize, string orderQueryParam) =>
        new(
            $"{collection.GenerateFlatCollectionId(urlRoots)}?page={lastPage}&pageSize={pageSize}{orderQueryParam}");
    
    public static string GenerateFullPath(this Hierarchy hierarchy, string itemSlug) => 
        $"{(hierarchy.Parent != null ? $"{hierarchy.Slug}/" : string.Empty)}{itemSlug}";
    
    public static string GetCollectionBucketKey(this Collection collection) =>
            $"{collection.CustomerId}/collections/{collection.Id}";

    public static async Task<string> GenerateUniqueIdAsync<T>(this DbSet<T> entities,
        int customerId, IIdGenerator idGenerator, CancellationToken cancellationToken = default)
        where T : class, IHierarchyResource
    {
        var isUnique = false;
        var id = string.Empty;
        var currentAttempt = 0;
        var random = new Random();
        var maxRandomValue = 25000;

        while (!isUnique)
        {
            if (currentAttempt > MaxAttempts)
            {
                throw new ConstraintException("Max attempts to generate an identifier exceeded");
            }

            id = idGenerator.Generate([
                customerId,
                DateTime.UtcNow.Ticks,
                random.Next(0, maxRandomValue)
            ]);

            isUnique = !await entities.AnyAsync(e => e.Id == id, cancellationToken);

            currentAttempt++;
        }

        return id;
    }
}
