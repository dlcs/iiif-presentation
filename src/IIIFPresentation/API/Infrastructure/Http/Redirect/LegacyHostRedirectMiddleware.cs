using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Services.Manifests.Settings;

namespace API.Infrastructure.Http.Redirect;

/// <summary>
/// Redirects requests received on the legacy presentation hostname (<see cref="PathSettings.LegacyPresentationApiUrl"/>)
/// to their equivalent path on the current hostname (customer-specific override, else the default).
/// </summary>
/// <remarks>
/// Of the requests that do get redirected (see below), GETs get a "301 - Moved Permanently"; PUT/POST/DELETE/PATCH
/// get a "308 - Permanent Redirect" since a "301" risks clients dropping the request body and switching the verb
/// to GET.
///
/// This is a plain host swap - path and query string carry over unchanged, even where the request would itself
/// redirect again once it reaches the new host (e.g. an unauthorised flat manifest/collection request still 303s
/// to its hierarchical url there, rather than that hop being folded into this one). A combined single-hop redirect
/// was considered but decided against - too much duplicated processing, and route values aren't populated yet at
/// the middleware stage - see https://github.com/dlcs/iiif-presentation/issues/653.
///
/// Requests carrying an "Authorization" header are never redirected, even though every other request is: browsers,
/// .NET's HttpClient and curl all strip the Authorization header when auto-following a redirect whose target has
/// a different host than the request that produced it (a deliberate anti-credential-leak behaviour, not something
/// a server response can override) - since presentation-api.* -&gt; iiif.* is exactly that, an authorised caller who
/// auto-follows the redirect would have its credentials silently dropped and get a 401 on the new host. Instead,
/// such requests are processed in place - as if they'd arrived on the canonical host - and the legacy host is
/// flagged as deprecated via the "Deprecation"/"Sunset"/"Link" response headers (RFC 9745/RFC 8594/IANA
/// "successor-version") instead of a redirect, so the caller keeps working today while being told to move off the
/// legacy host. "Deprecation" carries <see cref="PathSettings.LegacyHostnameCutoffDate"/> as its date when set
/// (else the bare "true"); "Sunset" is omitted unless <see cref="PathSettings.LegacyHostSunsetDate"/> is set.
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

        var canonicalLocation = BuildCanonicalLocation(context, targetHost);

        // Presence, not validity, is what matters here - even a header that would ultimately fail auth is still
        // stripped by the client's HTTP stack on a cross-host redirect follow, so there's nothing to gain by
        // parsing/validating it first
        if (!StringValues.IsNullOrEmpty(context.Request.Headers.Authorization))
        {
            await ProcessInPlaceWithDeprecationNotice(context, targetHost, canonicalLocation, settings);
            return;
        }

        logger.LogInformation("Redirecting legacy host request for {Path} to {Location}", context.Request.Path,
            canonicalLocation);

        context.Response.Headers.Location = canonicalLocation;
        context.Response.StatusCode = IsMutatingMethod(context.Request.Method)
            ? (int)HttpStatusCode.PermanentRedirect
            : (int)HttpStatusCode.MovedPermanently;

        await context.Response.CompleteAsync();
    }

    /// <summary>
    /// Lets the request continue down the pipeline rather than redirecting it, so its Authorization header is
    /// never at risk - but first swaps the request's Host/Scheme to <paramref name="targetHost"/>, so any
    /// downstream id/url generation (which reads the current host via IHttpContextAccessor) produces canonical-host
    /// urls exactly as if the request really had arrived there, and registers the deprecation-notice response
    /// headers to be added just before the response is sent.
    /// </summary>
    private async Task ProcessInPlaceWithDeprecationNotice(HttpContext context, Uri targetHost,
        string canonicalLocation, PathSettings settings)
    {
        logger.LogInformation(
            "Processing authorised legacy host request for {Path} in place; canonical location is {Location}",
            context.Request.Path, canonicalLocation);

        var originalHost = context.Request.Host;
        var originalScheme = context.Request.Scheme;
        context.Request.Host = HostString.FromUriComponent(targetHost);
        context.Request.Scheme = targetHost.Scheme;

        // RFC 9745 allows either the literal "true" or an HTTP-date of when deprecation took effect -
        // LegacyHostnameCutoffDate (the date new ids stopped being minted against the legacy host) doubles as that
        // date here, rather than introducing a second, near-identical setting just for this header
        var deprecationValue = settings.LegacyHostnameCutoffDate is { } cutoff ? ToHttpDate(cutoff) : "true";
        var sunsetValue = settings.LegacyHostSunsetDate?.UtcDateTime.ToString("R");

        context.Response.OnStarting(state =>
        {
            var (response, location, deprecation, sunset) = ((HttpResponse, string, string, string?))state;
            response.Headers.Append("Deprecation", deprecation);
            response.Headers.Append("Link", $"<{location}>; rel=\"successor-version\"");
            if (sunset is not null) response.Headers.Append("Sunset", sunset);
            return Task.CompletedTask;
        }, (context.Response, canonicalLocation, deprecationValue, sunsetValue));

        try
        {
            await next(context);
        }
        finally
        {
            // Restore values to what the client sent, instead of where they need to go - context is the live
            // shared HttpContext, so leaking the swap would corrupt logging/diagnostics on exception
            context.Request.Host = originalHost;
            context.Request.Scheme = originalScheme;
        }
    }

    private static string BuildCanonicalLocation(HttpContext context, Uri targetHost) =>
        new UriBuilder(targetHost)
        {
            Path = context.Request.Path.Value,
            Query = context.Request.QueryString.Value
        }.Uri.AbsoluteUri;

    /// <summary>
    /// Formats as an HTTP-date (e.g. "Fri, 01 Jan 2027 00:00:00 GMT") for the Deprecation/Sunset response headers.
    /// "R" alone doesn't convert to UTC first - it just labels whatever clock values the DateTime already holds as
    /// "GMT" - so this converts explicitly first, rather than risk a wrong date for a non-UTC configured value.
    /// </summary>
    private static string ToHttpDate(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc) // Unspecified - assume already-UTC, per convention
        };
        return utc.ToString("R");
    }

    private static bool IsMutatingMethod(string method) =>
        HttpMethods.IsPut(method) || HttpMethods.IsPost(method) || HttpMethods.IsDelete(method) ||
        HttpMethods.IsPatch(method);
}
