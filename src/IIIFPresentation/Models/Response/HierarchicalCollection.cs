using System.Text.Json.Serialization;

namespace Models.Response;

public class HierarchicalCollection
{
    [JsonPropertyName("@context")]
    public string Context { get; set; }
    
    public string Id { get; set; }
    
    public string Type { get; set; }
    
    public Dictionary<string, List<string>> Label { get; set; }
    
    public List<Item> Items { get; set; }
}