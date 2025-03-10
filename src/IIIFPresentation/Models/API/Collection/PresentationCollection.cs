
namespace Models.API.Collection;

public class PresentationCollection : IIIF.Presentation.V3.Collection, IPresentation
{
    public string? PublicId { get; set; }
    public string? FlatId { get; set; }
    
    public string? Slug { get; set; }
    
    public string? Parent { get; set; }
    
    public int? ItemsOrder { get; set; }

    public int? TotalItems { get; set; }

    public View? View { get; set; }

    public DateTime Created { get; set; }

    public DateTime Modified { get; set; }

    public string? CreatedBy { get; set; }

    public string? ModifiedBy { get; set; }

    public string? Tags { get; set; }
    
    public DescendantCounts? Totals { get; set; }
}
