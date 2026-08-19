using LazyCache;
using MediatR;

namespace API.Infrastructure.Requests.Pipelines;

/// <summary>
///     Interface for Mediator requests that invalidate cache records on success
/// </summary>
public interface IInvalidateCaches
{
    /// <summary>
    ///     Collection of cache keys invalidated by successful operation
    /// </summary>
    public string[] InvalidatedCacheKeys { get; }
}

/// <summary>
///     MediatR behaviour that will clear cacheKeys specified in request if request was successful
/// </summary>
public class CacheInvalidationBehaviour<TRequest, TResponse>(
    IAppCache appCache,
    ILogger<CacheInvalidationBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IInvalidateCaches, IRequest<TResponse>
    where TResponse : IModifyRequest
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var nextResponse = await next();

        if (nextResponse.IsSuccess) InvalidateCacheKeys(request);

        return nextResponse;
    }

    private void InvalidateCacheKeys(IInvalidateCaches request)
    {
        foreach (var cacheKey in request.InvalidatedCacheKeys)
        {
            logger.LogDebug("Invalidating cacheKey {CacheKey}", cacheKey);
            appCache.Remove(cacheKey);
        }
    }
}
