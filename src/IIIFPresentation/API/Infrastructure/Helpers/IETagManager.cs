namespace API.Infrastructure.Helpers;

public interface IETagManager
{
    public int CacheTimeoutSeconds { get; }
    
    bool TryGetETag(string id, out string? eTag);
    void UpsertETag(string id, string eTag);
}