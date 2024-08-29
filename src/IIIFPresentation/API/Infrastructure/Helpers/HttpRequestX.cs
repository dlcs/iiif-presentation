using API.Auth;

namespace API.Infrastructure.Helpers;

public static class HttpRequestX
{
    private static readonly KeyValuePair<string, string> AdditionalPropertiesHeader = new KeyValuePair<string, string>("IIIF-CS-Show-Extra", "All");

    public static bool ShowExtraProperties(this HttpRequest request)
    {
        return request.Headers.FirstOrDefault(x => x.Key == AdditionalPropertiesHeader.Key).Value == AdditionalPropertiesHeader.Value &&
               Authorizer.CheckAuthorized(request);
    }
}