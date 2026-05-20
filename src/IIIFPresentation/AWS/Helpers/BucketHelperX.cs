using Models.Database.Collections;

namespace AWS.Helpers;

public static class BucketHelperX
{
    private const string ManifestsSlug = "manifests";
    private const string CollectionsSlug = "collections";

    /// <summary>
    ///     Get key where this resource will be stored in S3
    /// </summary>
    public static string GetResourceBucketKey<T>(this T hierarchyResource, BucketLocationType locationType = BucketLocationType.Default)
        where T : IHierarchyResource
    {
        var slug = hierarchyResource is Manifest ? ManifestsSlug : CollectionsSlug;
        return GetResourceBucketKey(hierarchyResource.CustomerId, slug, hierarchyResource.Id, locationType);
    }

    /// <summary>
    ///     Get key where manifest with given id will be stored in S3 for provided customer
    /// </summary>
    public static string GetManifestBucketKey(int customerId, string flatId, BucketLocationType locationType = BucketLocationType.Default)
        => GetResourceBucketKey(customerId, ManifestsSlug, flatId, locationType);

    private static string GetResourceBucketKey(int customerId, string slug, string flatId, BucketLocationType locationType = BucketLocationType.Default)
        => $"{GetPrefixForLocationType(locationType)}{customerId}/{slug}/{flatId}";

    private static string GetPrefixForLocationType(BucketLocationType locationType) => locationType switch
    {
        BucketLocationType.Default => string.Empty,
        BucketLocationType.Staging => "staging/",
        BucketLocationType.Original => "original/",
        BucketLocationType.OriginalStaging => "original-staging/",
        _ => throw new ArgumentOutOfRangeException(nameof(locationType), locationType,
            "Unknown location type, cannot determine bucket prefix")
    };
}

/// <summary>
/// Allows determining the correct location of the resource in the S3 for read/write, based on the reason of storing.
/// </summary>
public enum BucketLocationType
{
    /// <summary>
    /// Default, this is the main entity storage
    /// </summary>
    Default,
    
    /// <summary>
    /// If background processing is required, the entity (e.g. manifest) is stored to a staging location until processing finishes
    /// </summary>
    Staging,
    
    /// <summary>
    /// For storing the original payload for any future use
    /// </summary>
    Original,
    
    /// <summary>
    /// For storing the original payload in a background processing scenario
    /// </summary>
    OriginalStaging
}
