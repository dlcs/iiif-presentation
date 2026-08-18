using LazyCache;
using Mediator;

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
///     Mediator behaviour that will clear cacheKeys specified in request if request was successful
/// </summary>
public class CacheInvalidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IInvalidateCaches, IRequest<TResponse>
    where TResponse : IModifyRequest
{
    private readonly IAppCache appCache;
    private readonly ILogger<CacheInvalidationBehaviour<TRequest, TResponse>> logger;

    public CacheInvalidationBehaviour(IAppCache appCache,
        ILogger<CacheInvalidationBehaviour<TRequest, TResponse>> logger)
    {
        this.appCache = appCache;
        this.logger = logger;
    }

    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var nextResponse = await next(request, cancellationToken);

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