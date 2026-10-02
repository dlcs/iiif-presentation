using Models.Database.General;

namespace Models.Database.Collections;

/// <summary>
/// Represents an item in the hierarchy
/// </summary>
public interface IHierarchyResource : IIdentifiable
{
    List<Hierarchy>? Hierarchy { get; }

    Guid Etag { get; }

    /// <summary>
    /// Created date/time
    /// </summary>
    DateTime Created { get; }

    /// <summary>
    /// Last modified date/time
    /// </summary>
    DateTime Modified { get; }

    /// <summary>
    /// Who created this resource
    /// </summary>
    string? CreatedBy { get; }

    /// <summary>
    /// Who last committed a change to this resource
    /// </summary>
    string? ModifiedBy { get; }
}
