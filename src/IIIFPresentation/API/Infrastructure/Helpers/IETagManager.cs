namespace API.Infrastructure.Helpers;

public interface IETagManager
{
    bool TryGetETag(string id, out string? eTag);
    void UpsertETag(string id, string eTag);
}