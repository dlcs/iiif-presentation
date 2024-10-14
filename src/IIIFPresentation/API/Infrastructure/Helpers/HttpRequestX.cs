using API.Auth;
using API.Infrastructure.Http;

namespace API.Infrastructure.Helpers;

public static class HttpRequestX
{
    private static readonly KeyValuePair<string, string> AdditionalPropertiesHeader = new (CustomHttpHeaders.ShowExtras, "All");

    [Obsolete("Use HasShowExtraHeader or DelegatedAuthenticator")]
    public static bool ShowExtraProperties(this HttpRequest request)
    {
        return request.HasShowExtraHeader() && Authorizer.CheckAuthorized(request);
    }
    
    public static bool HasShowExtraHeader(this HttpRequest request)
    {
        return request.Headers.FirstOrDefault(h => string.Equals(h.Key, AdditionalPropertiesHeader.Key, StringComparison.OrdinalIgnoreCase)).Value ==
               AdditionalPropertiesHeader.Value;
    }
}