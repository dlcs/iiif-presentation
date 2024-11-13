using IIIF.Presentation.V3.Strings;
using Models.Database.General;

namespace Models.Database.Collections;

public class Manifest : IHierarchyResource
{
    public required string Id { get; set; }
    
    /// <summary>
    /// The customer identifier
    /// </summary>
    public required int CustomerId { get; set; }
    
    /// <summary>
    /// Created date/time
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Last modified date/time
    /// </summary>
    public DateTime Modified { get; set; }

    /// <summary>
    /// Who created this Manifest
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Who last committed a change to this Manifest
    /// </summary>
    public string? ModifiedBy { get; set; }
    
    /// <summary>
    /// Manifest label
    /// </summary>
    public LanguageMap? Label { get; set; }
    
    public List<Hierarchy>? Hierarchy { get; set; }
}