namespace Core.Infrastructure;

/// <summary>
/// Custom IIIF-Presentation services, advertised in the "service" block of a resource
/// </summary>
public static class PresentationServices
{
    /// <summary>
    /// Search-across, run from a storage collection (see RFC 0008)
    /// </summary>
    public const string Search = "IIIFCS-Search";

    /// <summary>
    /// Search-across supporting label search only
    /// </summary>
    public const string SearchLevel0 = "level0";
}
