using System.Collections.Immutable;
using System.Net;
using API.Auth;
using API.Features.Manifest.Requests;
using API.Features.Storage.Requests;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using MediatR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Models.API.General;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using Services.Manifests.Settings;

namespace API.Infrastructure.Http.Redirect;

/// <summary>
/// Redirects requests received on the legacy presentation hostname (<see cref="PathSettings.LegacyPresentationApiUrl"/>)
/// to their equivalent on the current hostname (customer-specific override, else the default).
/// </summary>
/// <remarks>
/// GET requests get a "301 - Moved Permanently"; PUT/POST/DELETE/PATCH get a "308 - Permanent Redirect" since a
/// "301" risks clients dropping the request body and switching the verb to GET.
///
/// Where possible, a GET is redirected straight to its combined public location on the new host, rather than to
/// the equivalent url there, saving the extra "303 - See Other" hop that would otherwise happen when that url is
/// requested again on the new host: an anonymous/unauthorised GET of a flat manifest or collection redirects
/// straight to its public hierarchical url there; an authorised GET of a hierarchical manifest or collection
/// redirects straight to its flat url there. Every other GET (authorised flat, anonymous hierarchical) already
/// resolves in a single hop today, so gets a plain host swap.
///
/// No redirect is performed if there's no configured legacy hostname, or the request isn't for it.
/// </remarks>
public class LegacyHostRedirectMiddleware(
    RequestDelegate next,
    IOptions<PathSettings> pathSettings,
    IPathGenerator pathGenerator,
    ILogger<LegacyHostRedirectMiddleware> logger)
{
    private const string CustomerIdRouteValue = "customerId";

    // NOTE: IAuthenticator/IMediator/PresentationContext are resolved as method parameters, not constructor
    // dependencies - middleware registered via UseMiddleware<T>() is constructed once from the app's root service
    // provider, so a scoped service (IMediator resolves further scoped pipeline behaviours; PresentationContext is
    // a DbContext) would fail to resolve from there. Method injection resolves them from the current request's own
    // scope instead. IPathGenerator is a singleton (it reads the current host from IHttpContextAccessor on each
    // call, rather than being bound to one at construction) so it's safe as a constructor dependency.
    public async Task InvokeAsync(HttpContext context, IAuthenticator authenticator, IMediator mediator,
        PresentationContext dbContext)
    {
        var settings = pathSettings.Value;
        var legacyHost = settings.LegacyPresentationApiUrl?.Host;
        
        if (string.IsNullOrEmpty(legacyHost) ||
            !string.Equals(context.Request.Host.Host, legacyHost, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var pathElements = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var customerId = pathElements.Length > 0 && int.TryParse(pathElements[0], out var parsedCustomerId)
            ? parsedCustomerId
            : (int?)null;

        // GetPresentationUrl(customerId, created: null) resolves to "customer override if set, else
        // PresentationApiUrl" - the legacy-hostname branch it also handles never applies here, since we already
        // know we're on the legacy host
        var targetHost = customerId.HasValue
            ? settings.GetPresentationUrl(customerId.Value)
            : settings.PresentationApiUrl;

        // Whether this needs to swap between hierarchical and flat, fallback to just returning the correct URL if not -
        // only valid on GET requests
        var location = HttpMethods.IsGet(context.Request.Method)
            ? await TryGetCombinedRedirect(context, authenticator, mediator, dbContext, pathGenerator, customerId,
                  pathElements, targetHost) ?? BuildRedirectLocation(context, targetHost)
            : BuildRedirectLocation(context, targetHost);

        logger.LogDebug("Redirecting legacy host request for {Path} to {Location}", context.Request.Path, location);

        context.Response.Headers.Location = location;
        context.Response.StatusCode = IsMutatingMethod(context.Request.Method)
            ? (int)HttpStatusCode.PermanentRedirect
            : (int)HttpStatusCode.MovedPermanently;

        await context.Response.CompleteAsync();
    }

    private static bool IsMutatingMethod(string method) =>
        HttpMethods.IsPut(method) || HttpMethods.IsPost(method) || HttpMethods.IsDelete(method) ||
        HttpMethods.IsPatch(method);

    private static string BuildRedirectLocation(HttpContext context, Uri targetHost) =>
        $"{targetHost.Scheme}://{HostString.FromUriComponent(targetHost)}{context.Request.Path}{context.Request.QueryString}";

    /// <summary>
    /// For a GET whose response would otherwise itself be a redirect on the new host - an anonymous/unauthorised
    /// flat manifest/collection (would 303 to its hierarchical url), or an authorised hierarchical manifest/
    /// collection (would 303 to its flat url) - works out that eventual url directly, so the client doesn't need
    /// the further "303 - See Other" hop. Returns null when this doesn't apply - not a manifest/collection path,
    /// the request wouldn't itself redirect on the new host, or the resource has no such location - so the caller
    /// falls back to a plain host swap.
    /// </summary>
    private static async Task<string?> TryGetCombinedRedirect(HttpContext context, IAuthenticator authenticator,
        IMediator mediator, PresentationContext dbContext, IPathGenerator pathGenerator, int? customerId,
        string[] pathElements, Uri targetHost)
    {
        if (customerId is not { } id) return null;

        // null (rather than the type segment) when the path isn't 3 elements long
        var resourceTypeSegment = pathElements.Length == 3 ? pathElements[1] : null;

        // Case-insensitive to match how this segment is matched once actually routed on the target host
        var isManifestSlug =
            string.Equals(resourceTypeSegment, SpecConstants.ManifestsSlug, StringComparison.OrdinalIgnoreCase);
        var isCollectionSlug =
            string.Equals(resourceTypeSegment, SpecConstants.CollectionsSlug, StringComparison.OrdinalIgnoreCase);
        var isFlatResourcePath = isManifestSlug || isCollectionSlug;

        // Routing hasn't run yet at this point in the pipeline, so route values aren't populated - the
        // authenticator needs the customerId route value to validate credentials, so set it manually
        context.Request.RouteValues[CustomerIdRouteValue] = id;
        var authorised = await context.Request.IsAuthorisedForExtras(authenticator, context.RequestAborted);

        // If the request is flat and authed (or the opposite), then there's no need to perform the request redirect
        // as well (flat -> hierarchical or vice versa)
        if (isFlatResourcePath == authorised) return null;

        // Set the request host and scheme where they need to go, instead of where the client came from
        var originalHost = context.Request.Host;
        var originalScheme = context.Request.Scheme;
        context.Request.Host = HostString.FromUriComponent(targetHost);
        context.Request.Scheme = targetHost.Scheme;

        try
        {
            if (isFlatResourcePath) // anonymous/unauthorised flat manifest/collection -> its public hierarchical url
            {
                var resourceId = pathElements[2];
                return isManifestSlug
                    ? await GetManifestHierarchicalPath(mediator, id, resourceId)
                    : await GetCollectionHierarchicalPath(mediator, resourceId);
            }

            // authorised hierarchical manifest/collection -> its flat url
            var slug = string.Join('/', pathElements.Skip(1));
            return await GetHierarchicalResourceFlatPath(dbContext, pathGenerator, id, slug, context.Request.Query);
        }
        finally
        {
            // Restore values to what the client sent, instead of where they need to go
            context.Request.Host = originalHost;
            context.Request.Scheme = originalScheme;
        }
    }

    private static async Task<string?> GetManifestHierarchicalPath(IMediator mediator, int customerId, string id) =>
        (await mediator.Send(new GetManifest(customerId, id, ImmutableHashSet<Guid>.Empty, pathOnly: true)))
        .GetHierarchicalPath();

    private static async Task<string?> GetCollectionHierarchicalPath(IMediator mediator, string id) =>
        (await mediator.Send(new GetCollection(id, ImmutableHashSet<Guid>.Empty, page: null, pageSize: null,
            pathOnly: true)))
        .GetHierarchicalPath();

    /// <summary>
    /// The flat url for the manifest/collection at the given hierarchical slug, or null if there's no such
    /// resource. Mirrors the authorised branches of <c>StorageController.GetHierarchical</c>, but without that
    /// method's DLCS/S3 read of the resource's full content - only the (already cheap) hierarchy lookup used to
    /// resolve its type/id is needed to build the flat url
    /// </summary>
    private static async Task<string?> GetHierarchicalResourceFlatPath(PresentationContext dbContext,
        IPathGenerator pathGenerator, int customerId, string slug, IQueryCollection query)
    {
        var hierarchy = await dbContext.RetrieveHierarchy(customerId, slug);

        return hierarchy switch
        {
            { Type: ResourceType.IIIFManifest, ManifestId: not null } => pathGenerator.GenerateFlatId(hierarchy),
            { Type: ResourceType.IIIFCollection or ResourceType.StorageCollection } =>
                QueryHelpers.AddQueryString(pathGenerator.GenerateFlatId(hierarchy), query),
            _ => null
        };
    }
}
