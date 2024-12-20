namespace DLCS.Models;

public class Asset : JsonLdBase
{
    public int? Width { get; set; }
    
    public int? Height { get; set; }
    
    public bool Ingesting { get; set; }
}
