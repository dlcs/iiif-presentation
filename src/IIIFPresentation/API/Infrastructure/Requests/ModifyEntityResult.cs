using System.Diagnostics.CodeAnalysis;
using Core;
using IIIF;

namespace API.Infrastructure.Requests;

/// <summary>
///     Represents the result of a request to modify an entity
/// </summary>
/// <typeparam name="T">Type of entity being modified</typeparam>
/// <typeparam name="TError">Type of error</typeparam>
public class ModifyEntityResult<T, TError> : IModifyRequest
    where T : JsonLdBase
    where TError : Enum
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
    [MemberNotNullWhen(false, nameof(ErrorType))]
    [MemberNotNullWhen(true, nameof(Entity))]
    public bool IsSuccess { get; private init; }
    
    public TError? ErrorType { get; private init; }
    
    public Guid? ETag { get; private init; }

    public static ModifyEntityResult<T, TError> Failure(string error, TError errorType, WriteResult result = WriteResult.Unknown)
    {
        return new ModifyEntityResult<T, TError>
            { Error = error, WriteResult = result, IsSuccess = false, ErrorType = errorType };
    }
    
    public static ModifyEntityResult<T, TError> Success(T entity, WriteResult result = WriteResult.Updated, Guid? etag = null)
    {
        return new ModifyEntityResult<T, TError>
            { Entity = entity, WriteResult = result, IsSuccess = true, ETag = etag };
    }

}
