using System.Net;
using API.Infrastructure.Requests;
using Core.Web;
using Microsoft.Extensions.Options;
using Models.API.General;
using Repository.Paths;

namespace API.Infrastructure.Http.Redirect;

/// <summary>
/// Redirects trailing slash to the correct path
/// </summary>
public class TrailingSlashRedirectMiddleware(RequestDelegate next, 
    IPresentationPathGenerator presentationPathGenerator,
    IOptions<TypedPathTemplateOptions> settings,
    ILogger<TrailingSlashRedirectMiddleware> logger)
{
    private readonly TypedPathTemplateOptions settings = settings.Value;
    
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        if (context.Request.Method == HttpMethods.Get && (path?.EndsWith('/') ?? false) && path != "/")
        {
            logger.LogDebug("Trailing slash redirect detected for {Path}", path);
            var pathElements = path.Split('/');
            var customerIdIsInt = int.TryParse(pathElements[PathParser.FullPathCustomerIdIndex], out var customerId);

            string? completedPath;
            if (customerIdIsInt && pathElements.Length > PathParser.FullPathTypeIndex)
            {
                var pathType = pathElements[PathParser.FullPathTypeIndex];
                var presentationServiceType = WorkOutRedirectTemplate(pathType);

                if (presentationServiceType == PresentationResourceType.ResourcePublic)
                {
                    var hierarchicalPath = PathParser.GetHierarchicalPath(pathElements);

                    completedPath = presentationPathGenerator.GetHierarchyPresentationPathForRequest(
                        presentationServiceType, customerId, hierarchicalPath);

                    Uri.TryCreate(completedPath, UriKind.RelativeOrAbsolute, out var url);
                    var pathTemplateToCheck = GetPathTemplateToCheck(context, presentationServiceType);

                    // Route domain has an implicit trailing slash, so in this case, no redirect should be performed
                    if (url?.AbsolutePath == pathTemplateToCheck)
                    {
                        logger.LogDebug("Detected that this path is a route domain, so no redirect required - {Path}", completedPath);
                        completedPath = null;
                        await next(context);
                    }
                }
                else
                {
                    completedPath = presentationPathGenerator.GetFlatPresentationPathForRequest(presentationServiceType, customerId,
                        PathParser.GetResourceIdFromPath(pathElements));
                }
            }
            else
            {
                completedPath = context.Request.GetDisplayUrl(path: path.TrimEnd('/'));
            }

            if (completedPath != null)
            {
                logger.LogDebug("Completed redirect - {Path}", completedPath);
                context.Response.Headers.Append("Location", completedPath);
                context.Response.StatusCode = (int)HttpStatusCode.Found;

                await context.Response.CompleteAsync();
            }
        }
        else
        {
            await next(context);
        }
    }

    /// <summary>
    /// Gets a path template to check against in the correct format
    /// </summary>
    /// <remarks>
    /// This method essentially handles cases with additional path elements which act like the route domain,
    /// as well as the route domain directly
    /// </remarks>
    private string GetPathTemplateToCheck(HttpContext context, string presentationServiceType)
    {
        var template =
            settings.GetPathTemplateForHostAndType(context.Request.Host.Value, presentationServiceType);

        var templateToCheck = template.Split('/');
        var finalPathTemplate = string.Empty;

        foreach (var templateSection in templateToCheck)
        {
            // if the first character is '{', it's an interpreted template value, so we can stop checking
            if (templateSection.Length != 0 && templateSection.First().Equals('{'))
            {
                // length of 1 means that this path is matching against the route domain, without additional path elements
                // so '/' is acceptable, but "/test/test/" has to become "/test/test"
                if (finalPathTemplate.Length != 1)
                {
                    finalPathTemplate = finalPathTemplate.TrimEnd('/');
                }
                
                break;
            }

            finalPathTemplate += $"{templateSection}/";
        }

        return finalPathTemplate;
    }

    private string WorkOutRedirectTemplate(string? pathType) =>
        pathType switch
        {
            SpecConstants.ManifestsSlug => PresentationResourceType.ManifestPrivate,
            SpecConstants.CollectionsSlug => PresentationResourceType.CollectionPrivate,
            SpecConstants.CanvasesSlug => PresentationResourceType.Canvas,
            _ => PresentationResourceType.ResourcePublic // assume the path is hierarchical
        };
}
