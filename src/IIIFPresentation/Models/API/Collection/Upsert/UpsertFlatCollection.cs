using System.Diagnostics;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Models.API.Collection.Upsert;

public class UpsertFlatCollection
{
    public List<string> Behavior { get; set; } = [];

    public LanguageMap? Label { get; set; }
    
    public required string Slug { get; set; }
    
    public required string Parent { get; set; }
    
    public string? Tags { get; set; }
    
    public string? PresentationThumbnail { get; set; }
    
    [JsonProperty("thumbnail")]
    public object? ThumbnailFromRequest
    {
        set
        {
            if (value is not JArray)
            {
                PresentationThumbnail = value?.ToString();
            }
        }
    }
    
    public int? ItemsOrder { get; set; }
}