using AWS.Settings;
using Core.Web;
using Services.Manifests.Settings;

namespace BackgroundHandler.Settings;

public class BackgroundHandlerSettings
{
    public required AWSSettings AWS { get; set; }
    
    public required PathSettings PathSettings { get; set; }
    
    // public Uri PresentationApiUrl { get; set; }
    //
    // public Dictionary<int, Uri> CustomerPresentationApiUrl { get; set; } = new();
    //
    // /// <summary>
    // /// Get CustomerSpecificUrls, if found. 
    // /// </summary>
    // /// <param name="customerId">CustomerId to get settings for.</param>
    // /// <returns>Customer specific overrides, or default if not found.</returns>
    // public Uri GetCustomerSpecificPresentationUrl(int customerId)
    //     => CustomerPresentationApiUrl.TryGetValue(customerId, out var customerPresentationApiUrl)
    //         ? customerPresentationApiUrl
    //         : PresentationApiUrl;
    //
    // public TypedPathTemplateOptions PathRules { get; set; } = new ();
}
