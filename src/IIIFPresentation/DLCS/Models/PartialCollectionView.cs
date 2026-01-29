using Newtonsoft.Json;

namespace DLCS.Models;

public class PartialCollectionView : JsonLdBase
{
    [JsonProperty(Order = 11, PropertyName = "first")]
    public string? First { get; set; }

    [JsonProperty(Order = 12, PropertyName = "previous")]
    public string? Previous { get; set; }

    [JsonProperty(Order = 13, PropertyName = "next")]
    public string? Next { get; set; }

    [JsonProperty(Order = 14, PropertyName = "last")]
    public string? Last { get; set; }

    // These three properties are not part of the Hydra specification, but they are very handy.        
    [JsonProperty(Order = 21, PropertyName = "page")]
    public int Page { get; set; }
    
    [JsonProperty(Order = 22, PropertyName = "pageSize")]
    public int PageSize { get; set; }        
    
    [JsonProperty(Order = 23, PropertyName = "totalPages")]
    public int TotalPages { get; set; }
}
