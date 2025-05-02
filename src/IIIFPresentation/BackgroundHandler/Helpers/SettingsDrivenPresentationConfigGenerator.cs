using BackgroundHandler.Settings;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace BackgroundHandler.Helpers;

public class SettingsDrivenPresentationConfigGenerator(IOptions<BackgroundHandlerSettings> settings)
    : IPresentationPathGenerator
{
    private readonly BackgroundHandlerSettings settings = settings.Value;

    public string GetPresentationPathForRequest(string presentationServiceType, int? customerId, string? hierarchyPath, string? resourceId)
    {
        var host = settings.PresentationApiUrl;
        var template = settings.TypedPathTemplateOptions.GetPathTemplateForHostAndType(host, presentationServiceType);

        var path = PresentationPathReplacementHelpers.GeneratePresentationPathFromTemplate(template, customerId.ToString(), hierarchyPath, resourceId);
        
        if (!path.StartsWith('/')) path = '/' + path;
        
        return settings.PresentationApiUrl + path;
    }
}
