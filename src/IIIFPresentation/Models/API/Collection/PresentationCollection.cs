using Newtonsoft.Json.Linq;
using ExternalResource = IIIF.Presentation.V3.Content.ExternalResource;

namespace Models.API.Collection;

public class PresentationCollection : IIIF.Presentation.V3.Collection, IPresentation
{
    public string? PublicId { get; set; }
    
    public string? Slug { get; set; }
    
    public string? Parent { get; set; }
    
    public int? ItemsOrder { get; set; }

    public int TotalItems { get; set; }

    public View? View { get; set; }

    public DateTime Created { get; set; }

    public DateTime Modified { get; set; }

    public string? CreatedBy { get; set; }

    public string? ModifiedBy { get; set; }

    public string? Tags { get; set; }
    
    public string? PresentationThumbnail { get; set; }

    public new object? Thumbnail
    {
        get => base.Thumbnail;
        set
        {
            switch (value)
            {
                case JArray thumbnail:
                    base.Thumbnail = thumbnail.ToObject<List<ExternalResource>>();
                    break;
                case string presentationThumbnail:
                    PresentationThumbnail = presentationThumbnail;
                    break;
            }
        }
    }
}