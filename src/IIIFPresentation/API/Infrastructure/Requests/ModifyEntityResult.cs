using Core;
using IIIF;

namespace API.Infrastructure.Requests;

/// <summary>
///     Represents the result of a request to modify an entity
/// </summary>
/// <typeparam name="T">Type of entity being modified</typeparam>
public class ModifyEntityResult<T, TEnum> : IModifyRequest
    where T : JsonLdBase
{
    /// <summary>
    /// Enum representing overall result of operation
    /// </summary>
    public WriteResult WriteResult { get; private init; }

    /// <summary>
    /// Optional representation of entity
    /// </summary>
    public T? Entity { get; private init; }

    /// <summary>
    /// Optional error message if didn't succeed
    /// </summary>
    public string? Error { get; private init; }

    /// <summary>
    /// Explicit value stating success or failure
    /// </summary>
    public bool IsSuccess { get; private init; }
    
    public TEnum? ErrorType { get; private init; }
    
    public Guid? ETag { get; private init; }

    public static ModifyEntityResult<T, TEnum> Failure(string error, TEnum? errorType, WriteResult result = WriteResult.Unknown)
    {
        return new ModifyEntityResult<T, TEnum>
            { Error = error, WriteResult = result, IsSuccess = false, ErrorType = errorType };
    }
    
    public static ModifyEntityResult<T, TEnum> Success(T entity, WriteResult result = WriteResult.Updated, Guid? etag = null)
    {
        return new ModifyEntityResult<T, TEnum>
            { Entity = entity, WriteResult = result, IsSuccess = true, ETag = etag };
    }

}
