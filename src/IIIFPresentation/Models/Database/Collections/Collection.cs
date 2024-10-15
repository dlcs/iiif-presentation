using System.ComponentModel.DataAnnotations.Schema;
using IIIF.Presentation.V3.Strings;
using Models.Database.General;

namespace Models.Database.Collections;

public class Collection
{
    public required string Id { get; set; }

    /// <summary>
    /// Path element
    /// </summary>
    public required string Slug { get; set; }

    /// <summary>
    /// Whether the id (URL) of the stored Collection is its fixed id, or is the path from parent slugs. Each will redirect to the other if requested on the "wrong" canonical URL.
    /// </summary>
    public bool UsePath { get; set; }

    /// <summary>
    /// id of parent collection (Storage Collection or IIIF Collection)
    /// </summary>
    // public string? Parent { get; set; }

    /// <summary>
    /// Order within parent collection (unused if parent is storage)
    /// </summary>
    //public int? ItemsOrder { get; set; }

    /// <summary>
    /// Derived from the stored IIIF collection JSON - a single value on the default language
    /// </summary>
    public LanguageMap? Label { get; set; }

    /// <summary>
    /// Not the IIIF JSON, just a single path or URI to 100px, for rapid query results
    /// </summary>
    public string? Thumbnail { get; set; }

    /// <summary>
    /// User id if being edited
    /// </summary>
    public string? LockedBy { get; set; }

    /// <summary>
    /// Created date/time
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Last modified date/time
    /// </summary>
    public DateTime Modified { get; set; }

    /// <summary>
    /// Who last committed a change to this Collection
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Who last committed a change to this Collection
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Arbitrary strings to tag manifest, used to create virtual collections
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Is proper IIIF collection; will have JSON in S3
    /// </summary>
    public bool IsStorageCollection { get; set; }

    /// <summary>
    /// Whether the collection is available at Presentation.io/iiif/
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// The customer identifier
    /// </summary>
    public int CustomerId { get; set; }
    
    public List<Hierarchy>? Hierarchy { get; set; }

    /// <summary>
    /// The full path to this object, based on parent collections
    /// </summary>
    [NotMapped]
    public string? FullPath { get; set; }
}