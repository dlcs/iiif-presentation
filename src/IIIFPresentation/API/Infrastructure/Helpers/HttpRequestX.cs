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

    /// <summary>
    /// Retrieve the customer id
    ///
    /// NOTE: retrieved from route values
    /// </summary>
    /// <param name="request">The request to get the customer id from</param>
    /// <returns>A parsed customer id</returns>
    public static int? GetCustomerId(this HttpRequest request, ILogger logger)
    {
        var customerIdRouteValue = "customerId";
        
        if (!request.RouteValues.TryGetValue(customerIdRouteValue, out var customerIdRouteVal)
            || customerIdRouteVal is null)
        {
            logger.LogDebug("Unable to identify customerId in auth request to {Path}", request.Path);
            return null;
        }
        
        if (!int.TryParse(customerIdRouteVal.ToString(), out int customerId))
        {
            logger.LogDebug("Specified customerId is not numeric {Path}", request.Path);
            return null;
        }
        
        return customerId;
    }
}
