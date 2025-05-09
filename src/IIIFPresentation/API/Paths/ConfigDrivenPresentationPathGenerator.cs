using System.Text.Json;
using API.Helpers;
using API.Infrastructure.Requests;
using Core.Web;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace API.Paths;

public class ConfigDrivenPresentationPathGenerator(
    IOptions<TypedPathTemplateOptions> settings,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ConfigDrivenPresentationPathGenerator> logger)
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
        logger.LogDebug("Get path for presentation");
        logger.LogDebug("Overrides - {Overrides}", JsonSerializer.Serialize(settings.Overrides));
        
        var request = GetHttpRequest();
        logger.LogDebug("request host - {Request}", request.Host);
        var host = request.Host.Value;
        logger.LogDebug("host - {Host}", host);
        var template = settings.GetPathTemplateForHostAndType(host, presentationServiceType);
        logger.LogDebug("template - {Template}", template);

        var path = PresentationPathReplacementHelpers.GeneratePresentationPathFromTemplate(template,
            customerId.ToString(), hierarchyPath, resourceId);
        logger.LogDebug("path - {Path}", path);

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
