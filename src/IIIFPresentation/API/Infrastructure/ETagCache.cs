using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using API.Features.Manifest.Requests;
using API.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace API.Infrastructure;

public interface IETagCache
{
    bool IfNoneMatchForPath(string path, IImmutableSet<Guid> ifNoneMatch,
        [NotNullWhen(returnValue: true)] out Guid? etag);

    void SetEtagForPath(string path, Guid etag);
    void Invalidate(string? etag);
    void Invalidate(Guid etag);
}

public class ETagCache(IMemoryCache memoryCache, IOptionsMonitor<CacheSettings> settings) : IETagCache
{
    private static string EtagByPathKey(string path) => $"__etag_ebp:{path}";
    private static string PathByEtagKey(Guid etag) => $"__etag_pbe:{etag:N}";

    public bool IfNoneMatchForPath(string path, IImmutableSet<Guid> ifNoneMatch, [NotNullWhen(returnValue: true)] out Guid? etag)
    {
        etag = null;
        if (!memoryCache.TryGetValue(EtagByPathKey(path), out var cachedEtag)
            || cachedEtag is not Guid guid
            || !ifNoneMatch.Contains(guid)) return false;

        etag = guid;
        return true;
    }

    public void SetEtagForPath(string path, Guid etag)
    {
        memoryCache.Set(EtagByPathKey(path), etag, settings.CurrentValue.GetMemoryCacheOptions(CacheDuration.Short));
        memoryCache.Set(PathByEtagKey(etag), path, settings.CurrentValue.GetMemoryCacheOptions(CacheDuration.Short));
    }

    public void Invalidate(string? etag)
    {
        if(etag is null || !Guid.TryParse(etag.Trim('"'), out var guid))
            return;
        
        Invalidate(guid);
    }
    
    public void Invalidate(Guid etag)
    {
        var byEtagKey = PathByEtagKey(etag);
        var path = memoryCache.Get<string>(byEtagKey);
        if (path is null) return;
        memoryCache.Remove(byEtagKey);
        memoryCache.Remove(EtagByPathKey(path));
    }
}
