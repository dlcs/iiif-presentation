using Models.Database.Collections;

namespace AWS.Helpers;

public static class BucketHelperX
{
    private const string ManifestsSlug = "manifests";
    private const string CollectionsSlug = "collections";

    /// <summary>
    /// Get key where this resource will be stored in S3
    /// </summary>
    public static string GetResourceBucketKey<T>(this T hierarchyResource, BucketLocationType locationType = BucketLocationType.Default)
        where T : IHierarchyResource
    {
        var slug = hierarchyResource is Manifest ? ManifestsSlug : CollectionsSlug;
        return GetResourceBucketKey(hierarchyResource.CustomerId, slug, hierarchyResource.Id, locationType);
    }

    private static string GetResourceBucketKey(int customerId, string slug, string flatId, BucketLocationType locationType = BucketLocationType.Default)
    {
        var (prefix, suffix) = GetAffixesForLocationType(locationType);
        return $"{prefix}{customerId}/{slug}/{flatId}{suffix}";
    }

    private static (string Prefix, string Suffix) GetAffixesForLocationType(BucketLocationType locationType)
    {
        const string stagingPrefix = "staging/";
        const string originalSuffix = "/original";
        return locationType switch
        {
            BucketLocationType.Default => (string.Empty, string.Empty),
            BucketLocationType.Staging => (stagingPrefix, string.Empty),
            BucketLocationType.Original => (string.Empty, originalSuffix),
            BucketLocationType.OriginalStaging => (stagingPrefix, originalSuffix),
            _ => throw new ArgumentOutOfRangeException(nameof(locationType), locationType,
                "Unknown location type, cannot determine bucket affixes")
        };
    }
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
