using Models.Database.General;

namespace Models.Database.Collections;

/// <summary>
/// Represents an item in the hierarchy
/// </summary>
public interface IHierarchyResource : IIdentifiable
{
    /// <summary>
    /// The customer identifier
    /// </summary>
    int CustomerId { get; }
    
    List<Hierarchy>? Hierarchy { get; }
    
    public DateTime Created { get; set; }
}