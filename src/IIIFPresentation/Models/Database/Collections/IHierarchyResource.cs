using Models.Database.General;

namespace Models.Database.Collections;

/// <summary>
/// Represents an item in the hierarchy
/// </summary>
public interface IHierarchyResource
{
    string Id { get; }
    int CustomerId { get; }
    List<Hierarchy>? Hierarchy { get; }
}