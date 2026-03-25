namespace Repository.Helpers;

/// <summary>
/// retrieves and sets a customer id used by a global query filter
/// </summary>
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
