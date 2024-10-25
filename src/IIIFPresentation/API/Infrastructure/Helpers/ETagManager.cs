using API.Helpers;
using API.Settings;
using LazyCache;
using Microsoft.Extensions.Options;
using Models.Database.Collections;

namespace API.Infrastructure.Helpers;

public class ETagManager(IAppCache appCache, IOptionsMonitor<CacheSettings> cacheOptions, ILogger<ETagManager> logger)
    : IETagManager
{
    public int CacheTimeoutSeconds { get; } = appCache.DefaultCachePolicy.DefaultCacheDurationSeconds;

    /// <inheritdoc />
    public bool TryGetETag(string id, out string? eTag)
    {
        try
        {
            return appCache.TryGetValue(id, out eTag);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error retrieving ETag {EtagId}", id);
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
        appCache.Add(resourcePath, etag, cacheOptions.CurrentValue.GetMemoryCacheOptions());
    }
}