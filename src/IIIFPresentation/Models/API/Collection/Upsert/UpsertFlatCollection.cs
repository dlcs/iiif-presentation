using IIIF.Presentation.V3.Strings;

namespace Models.API.Collection.Upsert;

public class UpsertFlatCollection
{
    public List<string> Behavior { get; set; } = [];

    public LanguageMap? Label { get; set; }
    
    public required string Slug { get; set; }
    
    public required string Parent { get; set; }
    
    public string? Tags { get; set; }
    
    public string? Thumbnail { get; set; }
    
    public int? ItemsOrder { get; set; }
}