using Models.Database.Collections;
using Collection = Models.Database.Collections.Collection;
using Manifest = Models.Database.Collections.Manifest;

namespace Models.Database.General;

public class Hierarchy
{
    public int Id { get; set; }
    
    public string? CollectionId { get; set; }
    
    public virtual Collection? Collection { get; set; }
    
    public string? ManifestId { get; set; }
    
    public virtual Manifest? Manifest { get; set; }
    
    /// <summary>
    /// The type of the resource i.e.: storage collection, IIIF collection, manifest, etc
    /// </summary>
    public ResourceType Type { get; set; }
    
    /// <summary>
    /// The slug used on public requests
    /// </summary>
    public required string Slug { get; set; }
    
    /// <summary>
    /// The id of the parent record
    /// </summary>
    public string? Parent { get; set; }
    
    /// <summary>
    /// Used to determine the order of the item when viewed in a collection
    /// </summary>
    public int? ItemsOrder { get; set; }
    
    /// <summary>
    /// Whether this record is the canonical path for the collection or hierarchy
    /// </summary>
    public bool Canonical { get; set; } = true;
    
    /// <summary>
    /// The customer identifier
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// Id of related Collection or Manifest
    /// </summary>
    public string? ResourceId => CollectionId ?? ManifestId;
    
    /// <summary>
    /// The full path to this object, based on parent collections.
    /// e.g. parent/child or parent/child/grandchild
    /// </summary>
    public string? FullPath { get; set; }
}

public enum ResourceType
{
    StorageCollection = 0, 
    IIIFCollection = 1, 
    IIIFManifest = 2
}