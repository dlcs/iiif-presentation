using AWS.Settings;
using Services.Manifests.Settings;

namespace BackgroundHandler.Settings;

public class BackgroundHandlerSettings
{
    public required AWSSettings AWS { get; set; }
    
    public required PathSettings PathSettings { get; set; }
}
