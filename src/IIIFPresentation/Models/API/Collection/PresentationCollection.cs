
using Newtonsoft.Json;

namespace Models.API.Collection;

public class PresentationCollection : IIIF.Presentation.V3.Collection, IPresentation
{
    /// <summary>
    /// A collection of properties that are not part of the IIIF Presentation API spec and are custom to the
    /// IIIF-Presentation.
    /// </summary>
    public static readonly string[] PresentationPropertyKeys =
    [
        "slug", "publicId", "flatId", "parent", "itemsOrder", "totalItems",
        "created", "modified", "createdBy", "modifiedBy", "tags", "totals", "view"
    ];

    [JsonProperty(Order = 6)] public string? Slug { get; set; }
    [JsonProperty(Order = 7)] public string? PublicId { get; set; }
    [JsonProperty(Order = 8)] public string? Parent { get; set; }
    [JsonProperty(Order = 9)] public DateTime? Created { get; set; }

    [JsonProperty(Order = 9)] public DateTime? Modified { get; set; }

    [JsonProperty(Order = 10)] public string? CreatedBy { get; set; }

    [JsonProperty(Order = 10)] public string? ModifiedBy { get; set; }
    [JsonProperty(Order = 11)] public string? FlatId { get; set; }
    
    [JsonProperty(Order = 12)] public int? ItemsOrder { get; set; }

    [JsonProperty(Order = 13)] public int? TotalItems { get; set; }
    
    [JsonProperty(Order = 14)] public string? Tags { get; set; }
    
    [JsonProperty(Order = 15)] public DescendantCounts? Totals { get; set; }

    [JsonProperty(Order = 20)] public View? View { get; set; }
}
