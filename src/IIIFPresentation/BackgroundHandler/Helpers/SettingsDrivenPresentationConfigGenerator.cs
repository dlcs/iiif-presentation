using BackgroundHandler.Settings;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace BackgroundHandler.Helpers;

public class SettingsDrivenPresentationConfigGenerator(IOptions<BackgroundHandlerSettings> settings)
    : IPresentationPathGenerator
{
    private readonly BackgroundHandlerSettings settings = settings.Value;

    public string GetHierarchyPresentationPathForRequest(string presentationServiceType, int customerId, string hierarchyPath)
    {
        return GetPresentationPath(presentationServiceType, customerId, hierarchyPath);
    }
    
    public string GetFlatPresentationPathForRequest(string presentationServiceType, int customerId, string resourceId)
    {
        return GetPresentationPath(presentationServiceType, customerId, resourceId: resourceId);
    }

    public string GetPathCustomerIdAsStringForRequest(string presentationServiceType, string customerId, string path)
    {
        var template = GetFullTemplate(presentationServiceType);
        var replacedPath =
            PresentationPathReplacementHelpers.GeneratePresentationPathFromTemplate(template, customerId, hierarchyPath: path);
        
        return Uri.IsWellFormedUriString(replacedPath, UriKind.Absolute)
            ? replacedPath // template contains https://foo.com
            : settings.PresentationApiUrl + replacedPath;
    }

    private string GetPresentationPath(string presentationServiceType, int customerId, string? hierarchyPath = null,
        string? resourceId = null)
    {
        var template = GetFullTemplate(presentationServiceType);

        var path = PresentationPathReplacementHelpers.GeneratePresentationPathFromTemplate(template,
            customerId.ToString(), hierarchyPath, resourceId);
        
        return Uri.IsWellFormedUriString(path, UriKind.Absolute)
            ? path // template contains https://foo.com
            : settings.PresentationApiUrl + path;
    }
    
    private string GetFullTemplate(string presentationServiceType)
    {
        var host = settings.PresentationApiUrl;
        var template = settings.PathRules.GetPathTemplateForHostAndType(host, presentationServiceType);
        return template;
    }
}
