using BackgroundHandler.Settings;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace BackgroundHandler.Helpers;

public class SettingsDrivenPresentationConfigGenerator(IOptions<BackgroundHandlerSettings> settings)
    : IPresentationPathGenerator
{
    private readonly BackgroundHandlerSettings settings = settings.Value;

    public string GetHierarchyPresentationPathForRequest(string presentationServiceType, string customerId, string hierarchyPath)
    {
        return GetPresentationPath(presentationServiceType, customerId, hierarchyPath);
    }
    
    public string GetFlatPresentationPathForRequest(string presentationServiceType, string customerId, string resourceId)
    {
        return GetPresentationPath(presentationServiceType, customerId, resourceId: resourceId);
    }

    private string GetPresentationPath(string presentationServiceType, string customerId, string? hierarchyPath = null,
        string? resourceId = null)
    {
        var host = settings.PresentationApiUrl;
        var template = settings.PathRules.GetPathTemplateForHostAndType(host, presentationServiceType);

        var path = PresentationPathReplacementHelpers.GeneratePresentationPathFromTemplate(template,
            customerId, hierarchyPath, resourceId);
        
        if (!path.StartsWith('/')) path = '/' + path;
        
        return settings.PresentationApiUrl + path;
    }
}
