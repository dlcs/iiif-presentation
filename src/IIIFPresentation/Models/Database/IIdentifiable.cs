namespace Models.Database;

/// <summary>
/// Marks classes as having a string identifier field
/// </summary>
public interface IIdentifiable
{
    string Id { get; set; }
}