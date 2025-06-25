using IIIF.Presentation.V3.Strings;
using Models.Database.General;

namespace Models.Database.Collections;

public class Collection : IHierarchyResource, IHaveEtag
{
    public required string Id { get; set; }

    /// <summary>
    /// Whether the id (URL) of the stored Collection is its fixed id, or is the path from parent slugs. Each will redirect to the other if requested on the "wrong" canonical URL.
    /// </summary>
    public bool UsePath { get; set; }

    /// <summary>
    /// Derived from the stored IIIF collection JSON - a single value on the default language
    /// </summary>
    public LanguageMap? Label { get; set; }

    /// <summary>
    /// Not the IIIF JSON, just a single path or URI, for rapid query results
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
    /// Who created this Collection
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
    /// Marks whether this is "folder" IIIF collection; there is no JSON stored - everything DB driven
    /// </summary>
    public bool IsStorageCollection { get; set; }

    /// <summary>
    /// Whether the collection is publicly available
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// The customer identifier
    /// </summary>
    public int CustomerId { get; set; }
    
    public List<Hierarchy>? Hierarchy { get; set; }

    /// <summary>
    /// Navigation property for any children, when this Collection is the parent
    /// </summary>
    public IEnumerable<Hierarchy>? Children { get; set; }

    public Guid Etag { get; set; }
}

public static class CollectionX
{
    /// <summary>
    /// Check if <see cref="Collection"/> is root collection 
    /// </summary>
    public static bool IsRoot(this Collection collection) => KnownCollections.IsRoot(collection.Id);
}
