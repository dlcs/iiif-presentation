using Repository.Helpers;

namespace BackgroundHandler.Infrastructure;

public class MessageBasedCustomerIdProvider : ICustomerIdProvider
{
    private static readonly AsyncLocal<int?> CurrentCustomerId = new();
    
    public int GetCustomerId()
    {
        if (CurrentCustomerId.Value.HasValue)
        {
            return CurrentCustomerId.Value.Value;
        }
        
        return 0;
    }

    public void SetCustomerId(int customerId)
    {
        CurrentCustomerId.Value = customerId;
    }
}
