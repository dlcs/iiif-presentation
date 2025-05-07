using System.Net;
using API.Infrastructure.Validation;
using Repository.Paths;

namespace API.Infrastructure.Http.Redirect;

/// <summary>
/// Redirects trailing slash to the correct path
/// </summary>
public class TrailingSlashRedirectMiddleware(RequestDelegate next, IPresentationPathGenerator presentationPathGenerator)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        
        if (context.Request.Method == HttpMethods.Get && (path?.EndsWith('/') ?? false))
        {
            var pathElements = path.Split('/');
        
            var customerId = pathElements[1];
            
            // avoid issues with http//:something/
            var pathType = pathElements.Length < 3 ? string.Empty : pathElements[2];
        
            var presentationServiceType = WorkOutRedirectTemplate(pathType);

            var completedPath = presentationServiceType == PresentationResourceType.ResourcePublic
                ? presentationPathGenerator.GetHierarchyPresentationPathForRequest(presentationServiceType, customerId,
                    string.Join('/', pathElements.Skip(2).SkipLast(1))) // skip customer id and trailing whitespace
                : presentationPathGenerator.GetFlatPresentationPathForRequest(presentationServiceType, customerId,
                    pathElements.SkipLast(1).Last()); // miss the trailing whitespace and use the last path element
            
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
            _ => PresentationResourceType.ResourcePublic // assume the path is hierarchical
        };
}
