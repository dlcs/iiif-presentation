namespace Repository.Helpers;

public interface ICustomerIdProvider
{
    public int GetCustomerId();
    
    public void SetCustomerId(int customerId);
}
