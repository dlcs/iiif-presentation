using API.Helpers;
using Core.Web;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace API.Paths;

public class ConfigDrivenPresentationPathGenerator(
    IOptions<TypedPathTemplateOptions> settings,
    IHttpContextAccessor httpContextAccessor)
    : IPresentationPathGenerator
{
    private readonly TypedPathTemplateOptions settings = settings.Value;

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
        var request = GetHttpRequest();
        var host = request.Host.Value;
        var template = settings.GetPathTemplateForHostAndType(host, presentationServiceType);

        var path = PresentationPathReplacementHelpers.GeneratePresentationPathFromTemplate(template,
            customerId.ToString(), hierarchyPath, resourceId);

        return Uri.IsWellFormedUriString(path, UriKind.Absolute)
            ? path // template contains https://foo.com
            : request.GetDisplayUrl(path, includeQueryParams: false);
    }

    private HttpRequest GetHttpRequest()
    {
        var request = httpContextAccessor.SafeHttpContext().Request;
        return request;
    }
}
