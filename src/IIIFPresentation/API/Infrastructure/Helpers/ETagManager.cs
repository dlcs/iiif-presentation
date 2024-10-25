using API.Settings;
using LazyCache;
using Microsoft.Extensions.Options;

namespace API.Infrastructure.Helpers;

public class ETagManager(IAppCache appCache, IOptionsMonitor<CacheSettings> cacheOptions, ILogger<ETagManager> logger)
    : IETagManager
{
    public int CacheTimeoutSeconds { get; } = appCache.DefaultCachePolicy.DefaultCacheDurationSeconds;

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

    public void UpsertETag(string id, string etag)
    {
        appCache.Add(id, etag, cacheOptions.CurrentValue.GetMemoryCacheOptions());
    }
}