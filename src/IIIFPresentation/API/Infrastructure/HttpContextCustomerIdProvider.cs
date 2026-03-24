using Repository.Helpers;

namespace API.Infrastructure;

public class HttpContextCustomerIdProvider(IHttpContextAccessor httpContextAccessor) : ICustomerIdProvider
{
    private static readonly AsyncLocal<int?> CurrentCustomerId = new();
    private const string CustomerIdRouteValue = "customerId";
    private const int DefaultCustomer = 0; // Customer id with no values

    public int GetCustomerId()
    {
        // Check for preset value (testing/background jobs)
        if (CurrentCustomerId.Value.HasValue)
        {
            return CurrentCustomerId.Value.Value;
        }

        // Return default if no HTTP context (startup, migrations)
        if (httpContextAccessor.HttpContext == null)
        {
            return DefaultCustomer;
        }
        
        // Extract from route claims
        httpContextAccessor.HttpContext.Request.RouteValues.TryGetValue(CustomerIdRouteValue, out var customerIdRouteVal);
             
        if (!string.IsNullOrEmpty(customerIdRouteVal?.ToString()) && 
            int.TryParse(customerIdRouteVal.ToString(), out var customerId))
        {
            return customerId;
        }

        return DefaultCustomer;
    }

    public void SetCustomerId(int customerId)
    {
        CurrentCustomerId.Value = customerId;
    }
}
