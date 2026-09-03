using API.Auth;
using API.Infrastructure.Http;

namespace API.Infrastructure.Helpers;

public static class HttpRequestX
{
    private static readonly KeyValuePair<string, string> AdditionalPropertiesHeader = new (CustomHttpHeaders.ShowExtras, "All");
    private const string CreateSpaceHeader = "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"";

    /// <param name="request">The request to get the customer id from</param>
    extension(HttpRequest request)
    {
        /// <summary>
        /// Checks if the <see cref="HttpRequest"/> has appropriate header to show additional parameters 
        /// </summary>
        public bool HasShowExtraHeader()
        {
            return request.Headers.FirstOrDefault(h => string.Equals(h.Key, AdditionalPropertiesHeader.Key, StringComparison.OrdinalIgnoreCase)).Value ==
                   AdditionalPropertiesHeader.Value;
        }

        /// <summary>
        /// Checks whether this is an authorised request for additional (non-public) properties - i.e. it has the
        /// show-extras header, and passes authentication
        /// </summary>
        public async Task<bool> IsAuthorisedForExtras(IAuthenticator authenticator,
            CancellationToken cancellationToken = default)
            => request.HasShowExtraHeader() &&
               await authenticator.ValidateRequest(request, cancellationToken) == AuthResult.Success;

        /// <summary>
        /// Checks if the <see cref="HttpRequest"/> has header requesting a space be created 
        /// </summary>
        public bool HasCreateSpaceHeader()
            => request.Headers.Link.Contains(CreateSpaceHeader);

        /// <summary>
        /// Retrieve the customer id
        /// 
        /// NOTE: retrieved from route values
        /// </summary>
        /// <returns>A parsed customer id</returns>
        public int? GetCustomerId(ILogger logger)
        {
            const string customerIdRouteValue = "customerId";
        
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
}
