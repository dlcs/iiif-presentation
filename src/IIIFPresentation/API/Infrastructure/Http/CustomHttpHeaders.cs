namespace API.Infrastructure.Http;

internal static class CustomHttpHeaders
{
    /// <summary>
    /// HTTP header supplied to view extra values, in addition to basic IIIF Presentation payload 
    /// </summary>
    public const string ShowExtras = "X-IIIF-CS-Show-Extras";
}