using IIIF;

namespace Models.API.General;

public class Error : JsonLdBase
{
    public string Type => "Error";
    
    public string? Detail { get; set; }
    
    public int Status { get; set; }
}