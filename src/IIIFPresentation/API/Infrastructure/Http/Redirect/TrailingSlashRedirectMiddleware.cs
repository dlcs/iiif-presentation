using System.Net;
using API.Infrastructure.Validation;
using Repository.Paths;

namespace API.Infrastructure.Http.Redirect;

/// <summary>
/// Redirects trailing slash to the correct path
/// </summary>
public class TrailingSlashRedirectMiddleware(RequestDelegate next, IPresentationPathGenerator presentationPathGenerator)
{
    private const int CustomerIdIndex = 1;
    private const int PathTypeIndex = 2;
    
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        if (context.Request.Method == HttpMethods.Get && (path?.EndsWith('/') ?? false))
        {
            var pathElements = path.Split('/');
            var customerIdIsInt = int.TryParse(pathElements[CustomerIdIndex], out var customerId);

            string completedPath;
            if (customerIdIsInt && pathElements.Length > PathTypeIndex)
            {
                var pathType = pathElements[PathTypeIndex];
                var presentationServiceType = WorkOutRedirectTemplate(pathType);

                completedPath = presentationServiceType == PresentationResourceType.ResourcePublic
                    ? presentationPathGenerator.GetHierarchyPresentationPathForRequest(presentationServiceType, customerId,
                        string.Join('/', pathElements.Skip(2).SkipLast(1))) // skip customer id and trailing whitespace
                    : presentationPathGenerator.GetFlatPresentationPathForRequest(presentationServiceType, customerId,
                        pathElements.SkipLast(1).Last()); // miss the trailing whitespace and use the last path element
            }
            else
            {
                var trimmedPath = path.TrimEnd('/');
                completedPath = $"{context.Request.Scheme}://{context.Request.Host.Value}{trimmedPath}";
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
            _ => PresentationResourceType.ResourcePublic // assume the path is hierarchical
        };
}
