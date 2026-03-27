using API.Infrastructure.Helpers;
using Core.Exceptions;
using Repository.Helpers;

namespace API.Infrastructure;

/// <summary>
/// Retrieves a customer id from HTTPContext to use in 
/// </summary>
/// <param name="httpContextAccessor"></param>
public class HttpContextCustomerIdProvider(IHttpContextAccessor httpContextAccessor, ILogger<HttpContextCustomerIdProvider> logger) : ICustomerIdProvider
{
    public int GetCustomerId()
    {
        // Return default if no HTTP context (startup, migrations)
        if (httpContextAccessor.HttpContext == null)
        {
            throw new PresentationException("HTTP context is null");
        }

        var customerId = httpContextAccessor.HttpContext.Request.GetCustomerId(logger);
        if (customerId.HasValue) return  customerId.Value;

        throw new PresentationException("Could not resolve customerId from the URL");
    }

    public void SetCustomerId(int customerId)
    {
        throw new PresentationException("Cannot set a customer id to the current customer");
    }
}
