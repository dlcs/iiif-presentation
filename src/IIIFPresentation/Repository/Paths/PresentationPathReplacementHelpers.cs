namespace Repository.Paths;

/// <summary>
/// A collection of methods to make dealing with DLCS paths, and path replacements, easier
/// </summary>
public static class PresentationPathReplacementHelpers
{
    /// <summary>
    /// Replace known slugs in a path template.
    /// </summary>
    /// <param name="template">DLCS auth path template, including slugs to replace</param>
    /// <param name="customer">Value to replace {customer} with</param>
    /// <param name="hierarchyPath">Value to replace {hierarchyPath} with</param>
    /// <param name="resourceId">Value to replace {resourceId} with</param>
    /// <returns>Template with string replacements made</returns>
    public static string GeneratePresentationPathFromTemplate(
        string template,
        string? customer = null,
        string? hierarchyPath = null,
        string? resourceId = null)
    {
        if (!template.StartsWith("/")) template = "/" + template;
        
        return template
            .Replace("{customerId}", customer ?? string.Empty)
            .Replace("{hierarchyPath}", hierarchyPath?.TrimStart('/') ?? string.Empty)
            .Replace("{resourceId}", resourceId ?? string.Empty)
            .TrimEnd('/');
    }
}
