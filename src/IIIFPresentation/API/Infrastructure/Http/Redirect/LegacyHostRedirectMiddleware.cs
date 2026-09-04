using System.Net;
using Microsoft.Extensions.Options;
using Services.Manifests.Settings;

namespace API.Infrastructure.Http.Redirect;

/// <summary>
/// Redirects requests received on the legacy presentation hostname (<see cref="PathSettings.LegacyPresentationApiUrl"/>)
/// to their equivalent path on the current hostname (customer-specific override, else the default).
/// </summary>
/// <remarks>
/// GET requests get a "301 - Moved Permanently"; PUT/POST/DELETE/PATCH get a "308 - Permanent Redirect" since a
/// "301" risks clients dropping the request body and switching the verb to GET.
///
/// This is a plain host swap - path and query string carry over unchanged, even where the request would itself
/// redirect again once it reaches the new host (e.g. an unauthorised flat manifest/collection request still 303s
/// to its hierarchical url there, rather than that hop being folded into this one). A combined single-hop redirect
/// was considered but decided against - too much duplicated processing, and route values aren't populated yet at
/// the middleware stage - see https://github.com/dlcs/iiif-presentation/issues/653.
///
/// No redirect is performed if there's no configured legacy hostname, or the request isn't for it.
/// </remarks>
public class LegacyHostRedirectMiddleware(
    RequestDelegate next,
    IOptions<PathSettings> pathSettings,
    ILogger<LegacyHostRedirectMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
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

        var location = new UriBuilder(targetHost)
        {
            Path = context.Request.Path.Value,
            Query = context.Request.QueryString.Value
        }.Uri.AbsoluteUri;

        logger.LogInformation("Redirecting legacy host request for {Path} to {Location}", context.Request.Path, location);

        context.Response.Headers.Location = location;
        context.Response.StatusCode = IsMutatingMethod(context.Request.Method)
            ? (int)HttpStatusCode.PermanentRedirect
            : (int)HttpStatusCode.MovedPermanently;

        await context.Response.CompleteAsync();
    }

    private static bool IsMutatingMethod(string method) =>
        HttpMethods.IsPut(method) || HttpMethods.IsPost(method) || HttpMethods.IsDelete(method) ||
        HttpMethods.IsPatch(method);
}
