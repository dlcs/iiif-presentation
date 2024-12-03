using API.Infrastructure.Http;

namespace API.Infrastructure.Helpers;

public static class HttpRequestX
{
    private static readonly KeyValuePair<string, string> AdditionalPropertiesHeader = new (CustomHttpHeaders.ShowExtras, "All");
    private const string CreateSpaceHeader = "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"";

    /// <summary>
    /// Checks if the <see cref="HttpRequest"/> has appropriate header to show additional parameters 
    /// </summary>
    public static bool HasShowExtraHeader(this HttpRequest request)
    {
        return request.Headers.FirstOrDefault(h => string.Equals(h.Key, AdditionalPropertiesHeader.Key, StringComparison.OrdinalIgnoreCase)).Value ==
               AdditionalPropertiesHeader.Value;
    }

    /// <summary>
    /// Checks if the <see cref="HttpRequest"/> has header requesting a space be created 
    /// </summary>
    public static bool HasCreateSpaceHeader(this HttpRequest request)
        => request.Headers.Link.Contains(CreateSpaceHeader);
}