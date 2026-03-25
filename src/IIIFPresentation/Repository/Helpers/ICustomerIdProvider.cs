namespace Repository.Helpers;

public interface ICustomerIdProvider
{
    /// <summary>
    /// Retrieves the customer id for the current interaction
    /// </summary>
    public int GetCustomerId();
    
    /// <summary>
    /// Sets the customer id for this interaction
    /// </summary>
    public void SetCustomerId(int customerId);
}
