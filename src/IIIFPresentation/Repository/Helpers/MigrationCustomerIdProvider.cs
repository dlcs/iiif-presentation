using Core.Exceptions;

namespace Repository.Helpers;

public class MigrationCustomerIdProvider : ICustomerIdProvider
{
    public int GetCustomerId() => 0; // Default for migrations
    public void SetCustomerId(int customerId) =>
        throw new PresentationException(); // do not set the customer id for migrations
}
