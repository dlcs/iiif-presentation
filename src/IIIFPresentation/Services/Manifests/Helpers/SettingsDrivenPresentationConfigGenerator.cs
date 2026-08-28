using Core.Paths;
using Microsoft.Extensions.Options;
using Repository.Paths;
using Services.Manifests.Settings;

namespace Services.Manifests.Helpers;

/// <summary>
/// Implementation of <see cref="IPresentationPathGenerator"/> that uses customer configured PresentationApiUrl to
/// determine path templates, regardless of hostname. 
/// </summary>
public class SettingsDrivenPresentationConfigGenerator(IOptions<PathSettings> settings)
    : IPresentationPathGenerator
{
    private readonly PathSettings settings = settings.Value;

    public bool HasPathForCustomer(int customerId)
    {
        return settings.CustomerPresentationApiUrl.ContainsKey(customerId);
    }

    public string GetHierarchyPresentationPathForRequest(string presentationServiceType, int customerId,
        string hierarchyPath, DateTime? created = null)
    {
        return GetPresentationPath(presentationServiceType, customerId, created, hierarchyPath);
    }

    public string GetFlatPresentationPathForRequest(string presentationServiceType, int customerId, string resourceId,
        DateTime? created = null)
    {
        return GetPresentationPath(presentationServiceType, customerId, created, resourceId: resourceId);
    }

    private string GetPresentationPath(string presentationServiceType, int customerId, DateTime? created,
        string? hierarchyPath = null, string? resourceId = null)
    {
        var presentationUrl = settings.GetPresentationUrl(customerId, created);
        var template = settings.PathRules.GetPathTemplateForHostAndType(presentationUrl.Host, presentationServiceType);

        var path = template.GeneratePath(customerId, hierarchyPath, resourceId);
        
        return Uri.IsWellFormedUriString(path, UriKind.Absolute)
            ? path // template contains https://foo.com
            : new Uri(presentationUrl, path).ToString();
    }
}
