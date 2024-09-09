using IIIF.Presentation.V3.Strings;

namespace Models.API.Collection.Update;

public class UpdateFlatCollection
{
    public List<string> Behavior { get; set; } = new ();

    public LanguageMap? Label { get; set; }
    
    public required string Slug { get; set; }
    
    public required string Parent { get; set; }
    
    public string? Tags { get; set; }
    
    public string? Thumbnail { get; set; }
}