using API.Helpers;
using API.Infrastructure.Helpers;
using Core.Exceptions;
using Repository.Helpers;

namespace API.Infrastructure;

/// <summary>
/// Retrieves a customer id from HTTPContext to use in the customer id global query filter
/// </summary>
public class HttpContextCustomerIdProvider(IHttpContextAccessor httpContextAccessor, ILogger<HttpContextCustomerIdProvider> logger) : ICustomerIdProvider
{
    public int GetCustomerId()
    {
        var customerId = httpContextAccessor.SafeHttpContext().Request.GetCustomerId(logger);
        if (customerId.HasValue) return  customerId.Value;

        throw new PresentationException("Could not resolve customerId from the URL");
    }

    public void SetCustomerId(int customerId)
    {
        throw new PresentationException($"Setting a customer id is not allowed in the {nameof(HttpContextCustomerIdProvider)}");
    }
}
