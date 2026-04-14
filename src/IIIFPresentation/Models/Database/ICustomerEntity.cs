namespace Models.Database;

/// <summary>
/// Entity has a customer id
///
/// NOTE: implementing this interface causes a global query filter to be enabled on the customer id
/// </summary>
public interface ICustomerEntity
{
    public int CustomerId { get; set; }
}
