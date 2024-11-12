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
    private const string ManifestsSlug = "manifests";
    private const string CollectionsSlug = "collections";
    
    public static string GenerateHierarchicalCollectionId(this Collection collection, UrlRoots urlRoots) =>
        $"{urlRoots.BaseUrl}/{collection.CustomerId}{(string.IsNullOrEmpty(collection.FullPath) ? string.Empty : $"/{collection.FullPath}")}";

    public static string GenerateHierarchicalCollectionParent(this Collection collection, Hierarchy hierarchy, UrlRoots urlRoots)
    {
        var parentPath = collection.FullPath![..^hierarchy.Slug.Length].TrimEnd('/');

        return $"{urlRoots.BaseUrl}/{collection.CustomerId}{(string.IsNullOrEmpty(parentPath) ? string.Empty : $"/{parentPath}")}";
    }
    
    
    public static string GenerateFlatCollectionId(this Collection collection, UrlRoots urlRoots) =>
        $"{urlRoots.BaseUrl}/{collection.CustomerId}/collections/{collection.Id}";
    
    /// <summary>
    /// Get hierarchical id for current hierarchy item
    /// </summary>
    public static string GenerateHierarchicalId(this Hierarchy hierarchy, UrlRoots urlRoots) =>
        $"{urlRoots.BaseUrl}/{hierarchy.CustomerId}{(string.IsNullOrEmpty(hierarchy.FullPath) ? string.Empty : $"/{hierarchy.FullPath}")}";
    
    /// <summary>
    /// Get flat id for current hierarchy item
    /// </summary>
    public static string GenerateFlatId(this Hierarchy hierarchy, UrlRoots urlRoots) =>
        $"{urlRoots.BaseUrl}/{hierarchy.CustomerId}/{hierarchy.Type.GetSlug()}/{hierarchy.ResourceId}";
    
    /// <summary>
    /// Get flat id for parent of <see cref="Hierarchy"/> 
    /// </summary>
    public static string GenerateFlatParentId(this Hierarchy hierarchy, UrlRoots urlRoots) =>
        $"{urlRoots.BaseUrl}/{hierarchy.CustomerId}/{CollectionsSlug}/{hierarchy.Parent}";
    
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

    /// <summary>
    /// Get the FullPath of an item, using Canonical slug of attached Hierarcy collection and parent FullPath, if set 
    /// </summary>
    public static string GenerateFullPath(this Hierarchy collection, Collection parent)
        => GenerateFullPath(collection, parent.FullPath);
    
    /// <summary>
    /// Get the FullPath of an item, using Canonical slug of attached Hierarcy collection and provided parent 
    /// </summary>
    public static string GenerateFullPath(this Hierarchy hierarchy, string? parentPath) 
        => $"{(!string.IsNullOrEmpty(parentPath) ? $"{parentPath}/" : string.Empty)}{hierarchy.Slug}";
    
    
    /// <summary>
    /// Get Id for specified manifest
    /// </summary>
    public static string GenerateFlatManifestId(this Manifest manifest, UrlRoots urlRoots) =>
        $"{urlRoots.BaseUrl}/{manifest.CustomerId}/{ManifestsSlug}/{manifest.Id}";

    /// <summary>
    /// Get the ETag cache-key for resource 
    /// </summary>
    public static string GenerateETagCacheKey(this IHierarchyResource hierarchyResource)
        => $"/{hierarchyResource.CustomerId}/{hierarchyResource.GetSlug()}/{hierarchyResource.Id}";
    
    private static string GetSlug(this ResourceType resourceType) 
        => resourceType == ResourceType.IIIFManifest ? ManifestsSlug : CollectionsSlug;

    private static string GetSlug<T>(this T resource) where T : IHierarchyResource
        => resource is Manifest ? ManifestsSlug : CollectionsSlug;

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
