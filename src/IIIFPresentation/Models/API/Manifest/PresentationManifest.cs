using IIIF.Presentation.V3.Strings;
using Newtonsoft.Json;

namespace Models.API.Manifest;

public class PresentationManifest : IIIF.Presentation.V3.Manifest, IPresentation
{
    [JsonProperty(Order = 6)]
    public string? Slug { get; set; }
    [JsonProperty(Order = 7)]
    public string? Parent { get; set; }
    [JsonProperty(Order = 8)]
    public DateTime Created { get; set; }
    [JsonProperty(Order = 9)]
    public DateTime Modified { get; set; }
    [JsonProperty(Order = 10)]
    public string? CreatedBy { get; set; }
    [JsonProperty(Order = 11)]
    public string? ModifiedBy { get; set; }
    
    /// <summary>
    /// Represents, in a compact non-IIIF form, the relationship between a Content Resource and a Manifest.
    /// </summary>
    [JsonProperty(Order = 12)]
    public List<PaintedResource>? PaintedResources { get; set; }
    
}

/// <summary>
/// Details about the Content Resource on a canvas
/// </summary>
/// <remarks>This only contains CanvasPainting now but will be expanded to include Asset in future</remarks>
public class PaintedResource
{
    [JsonProperty(Order = 1)]
    public string Type => nameof(PaintedResource);
    
    [JsonProperty(Order = 2)]
    public required CanvasPainting CanvasPainting { get; set; }
}

public class CanvasPainting
{
    public required string CanvasId { get; set; }
    public string? CanvasOriginalId { get; set; }
    public int CanvasOrder { get; set; }
    public int? ChoiceOrder { get; set; }
    public string? Thumbnail { get; set; }
    public LanguageMap? Label { get; set; }
    public LanguageMap? CanvasLabel { get; set; }
    public string? Target { get; set; }
    public int? StaticWidth { get; set; }
    public int? StaticHeight { get; set; }
}