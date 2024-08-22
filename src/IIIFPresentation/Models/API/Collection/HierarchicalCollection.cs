using System.Text.Json.Serialization;

namespace Models.API.Collection;

public class HierarchicalCollection
{
    [JsonPropertyName("@context")]
    public required string Context { get; set; }
    
    public required string Id { get; set; }
    
    public PresentationType Type { get; set; }
    
    public Dictionary<string, List<string>>? Label { get; set; }
    
    public List<Item>? Items { get; set; }
}