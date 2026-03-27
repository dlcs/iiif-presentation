using Core.Exceptions;
using Repository.Helpers;

namespace BackgroundHandler.Infrastructure;

/// <summary>
/// Provider that requires the customer id to be set prior to retrieval
/// </summary>
public class SetCustomerIdProvider : ICustomerIdProvider
{
    private static readonly AsyncLocal<int?> CurrentCustomerId = new();
    
    public int GetCustomerId()
    {
        if (CurrentCustomerId.Value.HasValue)
        {
            return CurrentCustomerId.Value.Value;
        }

        throw new PresentationException("Customer id not set for retrieval");
    }

    public void SetCustomerId(int customerId)
    {
        CurrentCustomerId.Value = customerId;
    }
}
