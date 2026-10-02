using Core;
using IIIF;
using Models.API.General;

namespace API.Infrastructure.Requests;

/// <summary>
///     Represents the result of a request to modify a presentation resource.
///     Concrete alias for <see cref="ModifyEntityResult{TError}"/> with <see cref="ModifyCollectionType"/>.
/// </summary>
public class PresentationResult : ModifyEntityResult<ModifyCollectionType>
{
    public static PresentationResult Failure(string error, ModifyCollectionType errorType,
        WriteResult result = WriteResult.Unknown)
        => new() { Error = error, WriteResult = result, IsSuccess = false, ErrorType = errorType };

    public static PresentationResult Success(JsonLdBase entity, WriteResult result = WriteResult.Updated,
        Guid? etag = null)
        => new() { Entity = entity, WriteResult = result, IsSuccess = true, ETag = etag };
}
