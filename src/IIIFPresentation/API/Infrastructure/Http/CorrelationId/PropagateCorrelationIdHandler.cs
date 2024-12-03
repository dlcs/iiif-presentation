using Microsoft.Extensions.Http;
using Microsoft.Extensions.Primitives;

namespace API.Infrastructure.Http.CorrelationId;

/// <summary>
/// A DelegatingHandler that propagates x-correlation-id to downstream service
/// </summary>
public class PropagateCorrelationIdHandler(IHttpContextAccessor contextAccessor) : DelegatingHandler
{
    private const string CorrelationHeaderKey = "x-correlation-id";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (contextAccessor.HttpContext == null) return base.SendAsync(request, cancellationToken);

        var headerValue = contextAccessor.HttpContext.TryGetHeaderValue(CorrelationHeaderKey);
        if (!string.IsNullOrEmpty(headerValue))
        {
            AddCorrelationId(request, headerValue);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static void AddCorrelationId(HttpRequestMessage request, string? correlationId) 
        => request.Headers.TryAddWithoutValidation(CorrelationHeaderKey, [correlationId]);
}

internal static class HttpContextX
{
    /// <summary>
    /// Attempt to find specified header in current Request, optionally checking current Response
    /// </summary>
    public static string? TryGetHeaderValue(this HttpContext? httpContext, string headerKey, bool checkResponse = true)
    {
        if (httpContext == null) return null;
        
        if (TryGetHeaderValue(httpContext.Request.Headers, headerKey, out var fromRequest))
        {
            return fromRequest;
        }

        if (checkResponse && TryGetHeaderValue(httpContext.Response.Headers, headerKey, out var fromResponse))
        {
            return fromResponse;
        }
        
        return null;
    }
    
    private static bool TryGetHeaderValue(IHeaderDictionary headers, string headerKey, out string? correlationId)
    {
        correlationId = null;
        if (headers.TryGetValue(headerKey, out var values))
        {
            correlationId = values.FirstOrDefault();
            return !StringValues.IsNullOrEmpty(correlationId);
        }

        return false;
    }
}