using Models.Database.General;

namespace Models.Database.Collections;

public class Manifest
{
    public required string Id { get; set; }
    
    public required int CustomerId { get; set; }
    
    public List<Hierarchy>? Hierarchy { get; set; }
}