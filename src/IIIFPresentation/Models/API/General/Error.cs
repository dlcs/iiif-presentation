using IIIF;
using Newtonsoft.Json;

namespace Models.API.General;

public class Error : JsonLdBase
{
    public string? Title { get; set; }
    
    public string? Detail { get; set; }
    
    public string? Instance { get; set; }
    
    [JsonProperty("type")]
    public string? ErrorTypeUri { get; set; }
    
    public int Status { get; set; }
    
    public int? Code { get; set; }
}