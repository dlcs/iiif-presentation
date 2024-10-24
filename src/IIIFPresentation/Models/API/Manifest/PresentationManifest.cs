﻿namespace Models.API.Manifest;

public class PresentationManifest : IIIF.Presentation.V3.Manifest, IPresentation
{
    public string? Slug { get; set; }
    
    public string? Parent { get; set; }
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}