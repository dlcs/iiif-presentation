using AWS.Settings;

namespace BackgroundHandler.Settings;

public class BackgroundHandlerSettings
{
    public required AWSSettings AWS { get; set; }
    
    public string PresentationApiUrl { get; set; } = string.Empty;
}
