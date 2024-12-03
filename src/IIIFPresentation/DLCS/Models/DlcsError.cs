namespace DLCS.Models;

/// <summary>
/// Represents a problem object returned from DLCS
/// </summary>
/// <remarks>Note - expand with more fields as they're required</remarks>
internal class DlcsError
{
    public string? Description { get; set; }
}