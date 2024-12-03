namespace API.Infrastructure.Http;

/// <summary>
/// Set a customer user-agent string to all outgoing requests
/// </summary>
public class SetUserAgentHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Add("User-Agent", "DLCS/IIIF-Presentation");
        return base.SendAsync(request, cancellationToken);
    }
}