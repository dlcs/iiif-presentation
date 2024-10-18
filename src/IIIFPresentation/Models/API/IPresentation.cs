namespace Models.API;

/// <summary>
/// Custom IIIF-Presentation only fields added to IIIF resources.
/// </summary>
/// <remarks>Shared items should be added to this as required</remarks>
public interface IPresentation
{
    string? Slug { get; set; }
    string? Parent { get; set; }
}
