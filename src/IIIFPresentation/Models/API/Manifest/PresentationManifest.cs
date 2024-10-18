namespace Models.API.Manifest;

public class PresentationManifest : IIIF.Presentation.V3.Manifest, IPresentation
{
    public string? Slug { get; set; }
    
    public string? Parent { get; set; }
}