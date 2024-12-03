using Models.Database.General;

namespace Models.Database.Collections;

/// <summary>
/// Represents an item in the hierarchy
/// </summary>
public interface IHierarchyResource : IIdentifiable
{
    List<Hierarchy>? Hierarchy { get; }
    
    public DateTime Created { get; set; }
}