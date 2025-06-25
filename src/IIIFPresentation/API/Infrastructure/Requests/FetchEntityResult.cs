namespace API.Infrastructure.Requests;

/// <summary>
///     Represents the result of a request to load an entity
/// </summary>
/// <typeparam name="T">Type of entity being loaded</typeparam>
public class FetchEntityResult<T>
    where T : class
{
    /// <summary>
    ///     Optional representation of entity
    /// </summary>
    public T? Entity { get; private init; }

    /// <summary>
    ///     Optional error message if didn't succeed
    /// </summary>
    public string? ErrorMessage { get; private init; }

    /// <summary>
    ///     If true an error occured fetching resource
    /// </summary>
    public bool Error { get; private init; }

    /// <summary>
    ///     If true an error occured resources count not be found
    /// </summary>
    public bool EntityNotFound { get; private init; }
    
    public Guid? ETag { get; private init; }

    public static FetchEntityResult<T> Failure(string? errorMessage)
    {
        return new FetchEntityResult<T> { ErrorMessage = errorMessage, Error = true };
    }

    public static FetchEntityResult<T> NotFound(string? errorMessage = null)
    {
        return new FetchEntityResult<T> { ErrorMessage = errorMessage, EntityNotFound = true };
    }

    public static FetchEntityResult<T> Success(T entity, Guid? etag = null)
    {
        return new FetchEntityResult<T> { Entity = entity, ETag = etag };
    }

}
