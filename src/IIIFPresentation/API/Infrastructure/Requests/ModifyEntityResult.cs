using System.Diagnostics.CodeAnalysis;
using Core;
using IIIF;

namespace API.Infrastructure.Requests;

/// <summary>
///     Represents the result of a request to modify an entity
/// </summary>
/// <typeparam name="TError">Type of error</typeparam>
public class ModifyEntityResult<TError> : IModifyRequest
    where TError : Enum
{
    /// <summary>
    /// Enum representing overall result of operation
    /// </summary>
    public WriteResult WriteResult { get; protected init; }

    /// <summary>
    /// Optional representation of entity
    /// </summary>
    public JsonLdBase? Entity { get; protected init; }

    /// <summary>
    /// Optional error message if didn't succeed
    /// </summary>
    public string? Error { get; protected init; }

    /// <summary>
    /// Explicit value stating success or failure
    /// </summary>
    [MemberNotNullWhen(false, nameof(ErrorType))]
    [MemberNotNullWhen(true, nameof(Entity))]
    public bool IsSuccess { get; protected init; }

    public TError? ErrorType { get; protected init; }

    public Guid? ETag { get; protected init; }

    public static ModifyEntityResult<TError> Failure(string error, TError errorType, WriteResult result = WriteResult.Unknown)
    {
        return new ModifyEntityResult<TError>
            { Error = error, WriteResult = result, IsSuccess = false, ErrorType = errorType };
    }
    
    public static ModifyEntityResult<TError> Success(JsonLdBase entity, WriteResult result = WriteResult.Updated, Guid? etag = null)
    {
        return new ModifyEntityResult<TError>
            { Entity = entity, WriteResult = result, IsSuccess = true, ETag = etag };
    }

}
