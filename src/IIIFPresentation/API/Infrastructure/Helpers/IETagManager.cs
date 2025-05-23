using Models.Database.Collections;

namespace API.Infrastructure.Helpers;

public interface IETagManager
{
    /// <summary>
    /// Attempt to get ETag for specified id, where Id is the path of the resource
    /// </summary>
    bool TryGetETag(string resourcePath, out string? eTag);
    
    /// <summary>
    /// Attempt to get ETag for specified resource
    /// </summary>
    bool TryGetETag<T>(T resource, out string? eTag) where T : IHierarchyResource;
    
    /// <summary>
    /// Upsert ETag for specified path
    /// </summary>
    void UpsertETag(string resourcePath, string eTag);
}
