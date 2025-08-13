﻿using Core.Paths;
using Microsoft.Extensions.Options;
using Repository.Paths;
using Services.Manifests.Settings;

namespace Services.Manifests.Helpers;

public class SettingsDrivenPresentationConfigGenerator(IOptions<PathSettings> settings)
    : IPresentationPathGenerator
{
    private readonly PathSettings settings = settings.Value;

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
        var presentationUrl = settings.GetCustomerSpecificPresentationUrl(customerId);
        var template = settings.PathRules.GetPathTemplateForHostAndType(presentationUrl.Host, presentationServiceType);

        var path = PresentationPathReplacementHelpers.GeneratePresentationPathFromTemplate(template,
            customerId.ToString(), hierarchyPath, resourceId);
        
        return Uri.IsWellFormedUriString(path, UriKind.Absolute)
            ? path // template contains https://foo.com
            : new Uri(presentationUrl, path).ToString();
    }
}
