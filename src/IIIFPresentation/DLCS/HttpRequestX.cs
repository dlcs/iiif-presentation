using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace DLCS;

public static class HttpRequestX
{
    private const string AuthHeader = "Authorization";

    /// <summary>
    /// Attempt to get and parse "Authorization" header from specified httpRequest
    /// </summary>
    public static AuthenticationHeaderValue? TryGetValidAuthHeader(this HttpRequest request)
    {
        if (!request.Headers.TryGetValue(AuthHeader, out var value))
        {
            // Authorization header not in request
            return null;
        }

        if (!AuthenticationHeaderValue.TryParse(value, out AuthenticationHeaderValue? headerValue)
            || string.IsNullOrEmpty(headerValue.Parameter))
        {
            // Invalid Authorization header
            return null;
        }

        return headerValue;
    }
}