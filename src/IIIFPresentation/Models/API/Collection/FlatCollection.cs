using System.Text.Json.Serialization;
using IIIF.Presentation.V3.Strings;

namespace Models.API.Collection;

public class FlatCollection
{
    [JsonPropertyName("@context")]
    public List<string>? Context { get; set; }
    
    public string? Id { get; set; }
    
    public string? PublicId { get; set; }
    
    public PresentationType Type { get; set; }

    public List<string> Behavior { get; set; } = new ();

    public LanguageMap Label { get; set; } = null!;

    public string Slug { get; set; } = null!;
    
    public string? Parent { get; set; }
    
    public int? ItemsOrder { get; set; }
    
    public List<Item>? Items { get; set; }
    
    public List<PartOf>? PartOf { get; set; }
    
    public int TotalItems { get; set; }

    public View? View { get; set; }
    
    public List<SeeAlso>? SeeAlso { get; set; }
    
    public DateTime Created { get; set; }
    
    public DateTime Modified { get; set; }
    
    public string? CreatedBy { get; set; }
    
    public string? ModifiedBy { get; set; }
    
    public string? Tags { get; set; }
    
    public string? Thumbnail { get; set; }
}