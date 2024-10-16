using Collection = Models.Database.Collections.Collection;
using Manifest = Models.Database.Collections.Manifest;

namespace Models.Database.General;

public class Hierarchy
{
    public int Id { get; set; }

   // public string? ResourceId { get; set; }
   
    public string? CollectionId { get; set; }
    
    public virtual Collection? Collection { get; set; }
    
    public string? ManifestId { get; set; }
    
    public virtual Manifest? Manifest { get; set; }
    
    public ResourceType Type { get; set; }
    
    public required string Slug { get; set; }
    
    public string? Parent { get; set; }
    
    public int? ItemsOrder { get; set; }
    
    public bool Public { get; set; }
    
    public bool Canonical { get; set; }
    
    /// <summary>
    /// The customer identifier
    /// </summary>
    public int CustomerId { get; set; }
}

public enum ResourceType
{
    StorageCollection = 0, 
    IIIFCollection = 1, 
    Manifest = 2
}