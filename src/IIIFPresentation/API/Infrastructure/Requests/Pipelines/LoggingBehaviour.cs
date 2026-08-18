using System.Diagnostics;
using Mediator;

namespace API.Infrastructure.Requests.Pipelines;

/// <summary>
///     Mediator pipeline behaviour that logs requests with timings.
///     Will use ToString() property to log details
/// </summary>
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IBaseRequest
{
    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        // This could be cleverer, currently will just log ToString()
        logger.LogTrace("Handling '{RequestType}' request. {Request}", typeof(TRequest).Name, request);

        var sw = Stopwatch.StartNew();
        var response = await next(request, cancellationToken);
        sw.Stop();

        logger.LogTrace("Handled '{RequestType}' in {Elapsed}ms. {Request}", typeof(TRequest).Name,
            sw.ElapsedMilliseconds, request);

        return response;
    }
}
