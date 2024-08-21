using System.Text.Json.Serialization;

namespace Models.Response;

public class View
{
    [JsonPropertyName("@id")]
    public string Id { get; set; }

    [JsonPropertyName("@type")] 
    public PresentationType Type { get; set; }
    
    public int Page { get; set; }
    
    public int PageSize { get; set; }
    
    public int TotalPages { get; set; }
}