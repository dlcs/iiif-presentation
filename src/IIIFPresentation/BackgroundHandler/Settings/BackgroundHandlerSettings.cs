using AWS.Settings;
using Core.Web;

namespace BackgroundHandler.Settings;

public class BackgroundHandlerSettings
{
    public required AWSSettings AWS { get; set; }
    
    public string PresentationApiUrl { get; set; } = string.Empty;
    
    public Dictionary<string, string> CustomerPresentationApiUrl { get; set; } = new();
    
    /// <summary>
    /// Get CustomerSpecificUrls, if found. 
    /// </summary>
    /// <param name="customerId">CustomerId to get settings for.</param>
    /// <returns>Customer specific overrides, or default if not found.</returns>
    public string GetCustomerSpecificPresentationUrl(int customerId)
        => CustomerPresentationApiUrl.TryGetValue(customerId.ToString(), out var presentationApiUrl)
            ? presentationApiUrl
            : PresentationApiUrl;
    
    public TypedPathTemplateOptions PathRules { get; set; } = new ();
}
