using System.Text.Json.Serialization;

namespace Models.API.Collection;

public class HierarchicalCollection
{
    [JsonPropertyName("@context")]
    public string Context { get; set; }
    
    public string Id { get; set; }
    
    public PresentationType Type { get; set; }
    
    public Dictionary<string, List<string>> Label { get; set; }
    
    public List<Item> Items { get; set; }
}