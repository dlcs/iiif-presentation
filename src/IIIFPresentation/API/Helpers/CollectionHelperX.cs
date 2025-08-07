using API.Infrastructure.Validation;
using Models.API.General;
using Models.Database.Collections;

namespace API.Helpers;

/// <summary>
/// Collection of helpers to generate paths etc. for collections
/// </summary>
public static class CollectionHelperX
{
    private const string ManifestsSlug = "manifests";

    /// <summary>
    /// Get the ETag cache-key for resource 
    /// </summary>
    public static string GenerateETagCacheKey(this IHierarchyResource hierarchyResource)
        => $"/{hierarchyResource.CustomerId}/{hierarchyResource.GetSlug()}/{hierarchyResource.Id}";

    private static string GetSlug<T>(this T resource) where T : IHierarchyResource
        => resource is Manifest ? ManifestsSlug : SpecConstants.CollectionsSlug;
}
