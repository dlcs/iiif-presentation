using AWS.Settings;
using Core.Web;

namespace BackgroundHandler.Settings;

public class BackgroundHandlerSettings
{
    public required AWSSettings AWS { get; set; }
    
    public string PresentationApiUrl { get; set; } = string.Empty;
    
    public TypedPathTemplateOptions TypedPathTemplateOptions { get; set; }
}
