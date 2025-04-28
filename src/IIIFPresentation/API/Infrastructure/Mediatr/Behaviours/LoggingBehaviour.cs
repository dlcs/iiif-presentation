using System.Diagnostics;
using MediatR;

namespace API.Infrastructure.Mediatr.Behaviours;

/// <summary>
///     Mediatr Pipeline Behaviour that logs requests with timings.
///     Will use ToString() property to log details
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IBaseRequest
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        this.logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // This could be cleverer, currently will just log ToString()
        logger.LogTrace("Handling '{RequestType}' request. {Request}", typeof(TRequest).Name, request);

        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        logger.LogTrace("Handled '{RequestType}' in {Elapsed}ms. {Request}", typeof(TRequest).Name,
            sw.ElapsedMilliseconds, request);

        return response;
    }
}
