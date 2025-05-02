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

    public string GetPresentationPathForRequest(string presentationServiceType, int? customerId, string? hierarchyPath, string? resourceId)
    {
        var request = GetHttpRequest();
        var host = request.Host.Value;
        var template = settings.GetPathTemplateForHostAndType(host, presentationServiceType);

        var path = PresentationPathReplacementHelpers.GeneratePresentationPathFromTemplate(template, customerId.ToString(), hierarchyPath, resourceId);

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
