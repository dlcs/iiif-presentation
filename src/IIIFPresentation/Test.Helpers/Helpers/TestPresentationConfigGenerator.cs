using Core.Web;
using Manifests.Paths;
using Repository.Paths;

namespace Test.Helpers.Helpers;

public class TestPresentationConfigGenerator(string presentationUrl, TypedPathTemplateOptions typedPathTemplateOptions)
    : IPresentationPathGenerator
{
    public string GetHierarchyPresentationPathForRequest(string presentationServiceType, int customerId, string hierarchyPath)
    {
        return GetPresentationPath(presentationServiceType, customerId, hierarchyPath);
    }

    public string GetFlatPresentationPathForRequest(string presentationServiceType, int customerId, string resourceId)
    {
        return GetPresentationPath(presentationServiceType, customerId, resourceId: resourceId);
    }

    private string GetPresentationPath(string presentationServiceType, int customerId, string? hierarchyPath = null,
        string? resourceId = null)
    {
        var host = presentationUrl;
        var template = typedPathTemplateOptions.GetPathTemplateForHostAndType(host, presentationServiceType);

        var path = PresentationPathReplacementHelpers.GeneratePresentationPathFromTemplate(template,
            customerId.ToString(), hierarchyPath, resourceId);
        
        return Uri.IsWellFormedUriString(path, UriKind.Absolute)
            ? path // template contains https://foo.com
            : presentationUrl + path;
    }
}
