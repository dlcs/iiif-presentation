using Models.Database.Collections;

namespace AWS.Helpers;

public static class BucketHelperX
{
    private const string ManifestsSlug = "manifests";
    private const string CollectionsSlug = "collections";

    /// <summary>
    ///     Get key where this resource will be stored in S3
    /// </summary>
    public static string GetResourceBucketKey<T>(this T hierarchyResource, bool staging = false)
        where T : IHierarchyResource
    {
        var slug = hierarchyResource is Manifest ? ManifestsSlug : CollectionsSlug;
        return GetResourceBucketKey(hierarchyResource.CustomerId, slug, hierarchyResource.Id, staging);
    }

    /// <summary>
    ///     Get key where manifest with given id will be stored in S3 for provided customer
    /// </summary>
    public static string GetManifestBucketKey(int customerId, string flatId, bool staging = false)
        => GetResourceBucketKey(customerId, ManifestsSlug, flatId);

    private static string GetResourceBucketKey(int customerId, string slug, string flatId, bool staging = false)
        => $"{(staging ? "staging/" : string.Empty)}{customerId}/{slug}/{flatId}";
}
