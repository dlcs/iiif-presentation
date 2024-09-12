using System.Text.Json.Serialization;

namespace Models.API.Collection;

public class View
{
    [JsonPropertyName("@id")]
    public required string Id { get; set; }

    [JsonPropertyName("@type")] 
    public PresentationType Type { get; set; }
    
    public int Page { get; set; }
    
    public int PageSize { get; set; }
    
    public int TotalPages { get; set; }
    
    public string? Next { get; set; }
    
    public string? Last { get; set; }
}