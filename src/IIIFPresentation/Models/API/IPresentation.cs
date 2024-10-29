namespace Models.API;

/// <summary>
/// Custom IIIF-Presentation only fields added to IIIF resources.
/// </summary>
/// <remarks>Shared items should be added to this as required</remarks>
public interface IPresentation
{
    string? Slug { get; set; }
    string? Parent { get; set; }
    public DateTime Created { get; set; }

    public DateTime Modified { get; set; }

    public string? CreatedBy { get; set; }

    public string? ModifiedBy { get; set; }
}