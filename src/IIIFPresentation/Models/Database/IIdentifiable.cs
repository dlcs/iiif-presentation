namespace Models.Database;

/// <summary>
/// Marks classes as having a string identifier field and is for a specific Customer
/// </summary>
public interface IIdentifiable : ICustomerEntity
{
    string Id { get; }
}
