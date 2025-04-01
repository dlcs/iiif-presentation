using API.Helpers;
using API.Settings;
using LazyCache;
using Microsoft.Extensions.Options;
using Models.Database.Collections;

namespace API.Infrastructure.Helpers;

public class ETagManager(IAppCache appCache, IOptionsMonitor<CacheSettings> cacheOptions, ILogger<ETagManager> logger)
    : IETagManager
{
    /// <inheritdoc />
    public bool TryGetETag(string resourcePath, out string? eTag)
    {
        try
        {
            var found = appCache.TryGetValue(resourcePath, out eTag);
            logger.LogTrace("Etag for path {ResourcePath} - {Etag}. Found? {Found}", resourcePath, eTag, found);
            return found;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving ETag {ResourcePath}", resourcePath);
            eTag = null;
            return false;
        }
    }

    /// <inheritdoc />
    public bool TryGetETag<T>(T resource, out string? eTag) where T : IHierarchyResource
        => TryGetETag(resource.GenerateETagCacheKey(), out eTag);

    /// <inheritdoc />
    public void UpsertETag(string resourcePath, string etag)
    {
        logger.LogTrace("Caching etag {Etag} for {ResourcePath}", etag, resourcePath);
        appCache.Add(resourcePath, etag, cacheOptions.CurrentValue.GetNonExpiringMemoryCacheOptions());
    }
}
