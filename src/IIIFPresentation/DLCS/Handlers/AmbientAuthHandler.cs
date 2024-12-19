using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DLCS.Handlers;

/// <summary>
/// Delegating handler that adds any incoming authorization headers to outgoing request.
/// If no Auth header found, request is shortcut with 401 response.
/// </summary>
internal class AmbientAuthHandler(IHttpContextAccessor contextAccessor, ILogger<AmbientAuthHandler> logger)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var authHeader = contextAccessor.HttpContext.Request.TryGetValidAuthHeader();
        if (authHeader == null)
        {
            // If we have no auth don't bother making the downstream request as it'll fail
            logger.LogDebug("Authentication header not found, aborting request for {Path}", request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }
        
        request.Headers.Authorization = authHeader;
        return base.SendAsync(request, cancellationToken);
    }
}
