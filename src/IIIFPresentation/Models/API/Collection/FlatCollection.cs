using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using Newtonsoft.Json;

namespace Models.API.Collection;

public class FlatCollection : IIIF.Presentation.V3.Collection
{
    [JsonProperty("@context")]
    public new List<string>? Context
    {
        get => base.Context as List<string>;
        set => base.Context = value;
    }

    #region Overrides of Collection

    public override string Type { get; } = nameof(FlatCollection);

    #endregion

    public string? PublicId { get; set; }

    public PresentationType PresentationType { get; set; }

    public required string Slug { get; set; }

    public string? Parent { get; set; }

    public int? ItemsOrder { get; set; }

    public int TotalItems { get; set; }

    public View? View { get; set; }

    public DateTime Created { get; set; }

    public DateTime Modified { get; set; }

    public string? CreatedBy { get; set; }

    public string? ModifiedBy { get; set; }

    public string? Tags { get; set; }
}