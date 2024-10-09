
using Models.Database.Collections;

namespace Models.Database.General;

public class Hierarchy
{
    public int Id { get; set; }

    public string? CollectionId { get; set; }
    
    public Collection? Collection { get; set; }
    
    public string? ManifestId { get; set; }
    
    public Manifest? Manifest { get; set; }
    public required string Slug { get; set; }
    
    public string? Parent { get; set; }
    
    public int? ItemsOrder { get; set; }
    
    public bool Public { get; set; }
    
    public bool Canonical { get; set; }
}