using System.Net;
using API.Infrastructure.Requests;
using API.Infrastructure.Validation;
using Repository.Paths;

namespace API.Infrastructure.Http.Redirect;

/// <summary>
/// Redirects trailing slash to the correct path
/// </summary>
public class TrailingSlashRedirectMiddleware(RequestDelegate next, 
    IPresentationPathGenerator presentationPathGenerator,
    ILogger<TrailingSlashRedirectMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        if (context.Request.Method == HttpMethods.Get && (path?.EndsWith('/') ?? false))
        {
            logger.LogDebug("Trailing slash redirect detected");
            var pathElements = path.Split('/');
            var customerIdIsInt = int.TryParse(pathElements[PathParser.FullPathCustomerIdIndex], out var customerId);

            string completedPath;
            if (customerIdIsInt && pathElements.Length > PathParser.FullPathTypeIndex)
            {
                var pathType = pathElements[PathParser.FullPathTypeIndex];
                var presentationServiceType = WorkOutRedirectTemplate(pathType);

                completedPath = presentationServiceType == PresentationResourceType.ResourcePublic
                    ? presentationPathGenerator.GetHierarchyPresentationPathForRequest(presentationServiceType, customerId,
                        PathParser.GetHierarchicalPath(pathElements))
                    : presentationPathGenerator.GetFlatPresentationPathForRequest(presentationServiceType, customerId,
                        PathParser.GetResourceIdFromPath(pathElements));
                logger.LogDebug("Completed path - {Path}", completedPath);
            }
            else
            {
                completedPath = context.Request.GetDisplayUrl(path: path.TrimEnd('/'));
            }

            context.Response.Headers.Append("Location", completedPath);
            context.Response.StatusCode = (int)HttpStatusCode.Found;
            
            await context.Response.CompleteAsync();
        }
        else
        {
            await next(context);
        }
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
