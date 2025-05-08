using API.Helpers;
using API.Infrastructure.Requests;
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

    public string GetPathCustomerIdAsStringForRequest(string presentationServiceType, string customerId, string path)
    {
        var (request, template) = GetRequiredValues(presentationServiceType);
        var replacedPath =
            PresentationPathReplacementHelpers.GeneratePresentationPathFromTemplate(template, customerId,  hierarchyPath: path);
        
        return Uri.IsWellFormedUriString(replacedPath, UriKind.Absolute)
            ? replacedPath // template contains https://foo.com
            : request.GetDisplayUrl(replacedPath, includeQueryParams: false);
    }

    private string GetPresentationPath(string presentationServiceType, int customerId, string? hierarchyPath = null, 
        string? resourceId = null)
    {
        var (request, template) = GetRequiredValues(presentationServiceType);

        var path = PresentationPathReplacementHelpers.GeneratePresentationPathFromTemplate(template,
            customerId.ToString(), hierarchyPath, resourceId);

        return Uri.IsWellFormedUriString(path, UriKind.Absolute)
            ? path // template contains https://foo.com
            : request.GetDisplayUrl(path, includeQueryParams: false);
    }

    private (HttpRequest request, string template) GetRequiredValues(string presentationServiceType)
    {
        var request = GetHttpRequest();
        var host = request.Host.Value;
        var template = settings.GetPathTemplateForHostAndType(host, presentationServiceType);
        return (request, template);
    }

    private HttpRequest GetHttpRequest()
    {
        var request = httpContextAccessor.SafeHttpContext().Request;
        return request;
    }
}
