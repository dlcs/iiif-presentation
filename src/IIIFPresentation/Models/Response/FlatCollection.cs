using System.Text.Json.Serialization;

namespace Models.Response;

public class FlatCollection
{
    [JsonPropertyName("@context")]
    public List<string> Context { get; set; }
    
    public string Id { get; set; }
    
    public string PublicId { get; set; }
    
    public string Type { get; set; }
    
    public string Behaviour { get; set; }
    
    public Dictionary<string, List<string>> Label { get; set; }
    
    public string Slug { get; set; }
    
    public string? Parent { get; set; }
    
    public int? ItemsOrder { get; set; }
    
    public List<Item> Items { get; set; }
    
    public List<PartOf>? PartOf { get; set; }
    
    public int TotalItems { get; set; }

    public View View { get; set; } = new View();
    
    public List<SeeAlso> SeeAlso { get; set; }
    
    public DateTime Created { get; set; }
    
    public DateTime Modified { get; set; }
    
    public string? CreatedBy { get; set; }
    
    public string? ModifiedBy { get; set; }
}