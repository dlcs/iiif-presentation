﻿using IIIF.Presentation.V3.Strings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Models.API.Manifest;

public class PresentationManifest : IIIF.Presentation.V3.Manifest, IPresentation
{
    [JsonProperty(Order = 6)] public string? Slug { get; set; }
    [JsonProperty(Order = 7)] public string? PublicId { get; set; }
    [JsonProperty(Order = 8)] public string? Parent { get; set; }
    [JsonProperty(Order = 9)] public DateTime Created { get; set; }
    [JsonProperty(Order = 9)] public DateTime Modified { get; set; }
    [JsonProperty(Order = 10)] public string? CreatedBy { get; set; }
    [JsonProperty(Order = 10)] public string? ModifiedBy { get; set; }
    [JsonProperty(Order = 11)] public string? FlatId { get; set; }

    /// <summary>
    /// Represents, in a compact non-IIIF form, the relationship between a Content Resource and a Manifest.
    /// </summary>
    [JsonProperty(Order = 12)]
    public List<PaintedResource>? PaintedResources { get; set; }

    [JsonProperty(Order = 13)] public string? Space { get; set; }
    
    /// <summary>
    /// Contains details of the ingestion progress
    /// </summary>
    [JsonProperty(Order = 14)] public IngestingAssets? Ingesting { get; set; }

    [JsonIgnore] public string? FullPath { get; set; }
    
    /// <summary>
    /// Whether this manifest contains items that are currently being ingested
    /// </summary>
    [JsonIgnore] public bool CurrentlyIngesting { get; set; }
}

/// <summary>
/// Details about the Content Resource on a canvas
/// </summary>
public class PaintedResource
{
    [JsonProperty(Order = 1)]
    public string Type => nameof(PaintedResource);
    
    [JsonProperty(Order = 2)]
    public CanvasPainting? CanvasPainting { get; set; }
    
    [JsonProperty(Order = 3)]
    public JObject? Asset { get; set; }
}

public class CanvasPainting
{
    public string? CanvasId { get; set; }
    public string? CanvasOriginalId { get; set; }
    public int? CanvasOrder { get; set; }
    public int? ChoiceOrder { get; set; }
    public string? Thumbnail { get; set; }
    public LanguageMap? Label { get; set; }
    public LanguageMap? CanvasLabel { get; set; }
    public string? Target { get; set; }
    public int? StaticWidth { get; set; }
    public int? StaticHeight { get; set; }
    public double? Duration { get; set; }
}

public class IngestingAssets
{
    public int Total { get; set; }
    public int Finished { get; set; }
    public int Errors { get; set; }
}
