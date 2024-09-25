using Newtonsoft.Json;

namespace Models.API.Collection;

public class View
{
    [JsonProperty("@id")]
    public required string Id { get; set; }

    [JsonProperty("@type")] 
    public PresentationType Type { get; set; }
    
    public int Page { get; set; }
    
    public int PageSize { get; set; }
    
    public int TotalPages { get; set; }
    
    public Uri? Next { get; set; }
    
    public Uri? Previous { get; set; }
    
    public Uri? First { get; set; }
    
    public Uri? Last { get; set; }
}