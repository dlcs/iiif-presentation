using LazyCache;
using Microsoft.Extensions.Caching.Memory;

namespace API.Infrastructure.Helpers;

public class ETagManager(IAppCache appCache, ILogger<ETagManager> logger) : IETagManager
{
    MemoryCacheEntryOptions options = new MemoryCacheEntryOptions().SetSize(1);
    
    public bool TryGetETag(string id, out string? eTag)
    {
        try
        {
            return appCache.TryGetValue(id, out eTag);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error retrieving ETag");
            eTag = null;
            return false;
        }
    }
    
    public void UpsertETag(string id, string etag)
    {
        appCache.Add(id, etag, options);
    }
}

public interface IETagManager
{
    bool TryGetETag(string id, out string? eTag);
    void UpsertETag(string id, string eTag);
}