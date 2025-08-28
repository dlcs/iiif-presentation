using Core.Web;

namespace Services.Manifests.Settings;

public class PathSettings
{
    public const string SettingsName = "PathSettings";
    
    public required Uri PresentationApiUrl { get; set; }
    
    public Dictionary<int, Uri> CustomerPresentationApiUrl { get; set; } = new();
    
    /// <summary>
    /// Get CustomerSpecificUrls, if found. 
    /// </summary>
    /// <param name="customerId">CustomerId to get settings for.</param>
    /// <returns>Customer specific overrides, or default if not found.</returns>
    public Uri GetCustomerSpecificPresentationUrl(int customerId)
        => CustomerPresentationApiUrl.GetValueOrDefault(customerId, PresentationApiUrl);
    
    public TypedPathTemplateOptions PathRules { get; set; } = new ();
}
