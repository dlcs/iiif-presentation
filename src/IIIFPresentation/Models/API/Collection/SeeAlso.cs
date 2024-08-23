using IIIF.Presentation.V3.Strings;

namespace Models.API.Collection;

public class SeeAlso
{
    public required string Id { get; set; }

    public PresentationType Type { get; set; }
    
    public LanguageMap? Label { get; set; }
    
    public List<string>? Profile { get; set; }
}