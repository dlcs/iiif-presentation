using System.Text.Json.Serialization;

namespace Models.Response;

public class View
{
    [JsonPropertyName("@id")]
    public string Id { get; set; }

    [JsonPropertyName("@type")] 
    public string Type { get; set; } = "PartialCollectionView";
    
    public int Page { get; set; }
    
    public int PageSize { get; set; }
    
    public int TotalPages { get; set; }
}