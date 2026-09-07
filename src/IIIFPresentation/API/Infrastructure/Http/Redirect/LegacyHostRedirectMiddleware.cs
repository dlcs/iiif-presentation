using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Services.Manifests.Settings;

namespace API.Infrastructure.Http.Redirect;

/// <summary>
/// Redirects requests received on the legacy presentation hostname (<see cref="PathSettings.LegacyPresentationApiUrl"/>)
/// to their equivalent path on the default current hostname (<see cref="PathSettings.PresentationApiUrl"/>) - never
/// a customer-specific <see cref="PathSettings.CustomerPresentationApiUrl"/> override, since those are reverse-proxy
/// hosts with their own independent path-rewriting rules that this app can't redirect straight to a canonical path
/// on.
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
/// legacy host. "Deprecation" always carries a Structured-Fields Date - <see cref="PathSettings.LegacyHostnameCutoffDate"/>
/// when set, else <see cref="DefaultDeprecationDate"/> - RFC 9745 has no "unknown date" form to omit it with,
/// unlike "Sunset", which is omitted unless <see cref="PathSettings.LegacyHostSunsetDate"/> is set.
/// </remarks>
public class LegacyHostRedirectMiddleware(
    RequestDelegate next,
    IOptions<PathSettings> pathSettings,
    ILogger<LegacyHostRedirectMiddleware> logger)
{
    /// <summary>
    /// Fallback date for the "Deprecation" header when <see cref="PathSettings.LegacyHostnameCutoffDate"/> isn't
    /// configured - RFC 9745 requires "Deprecation" to always carry a date, unlike "Sunset" which can simply be
    /// omitted. Deliberately a constant here rather than a default value on <see cref="PathSettings.LegacyHostnameCutoffDate"/>
    /// itself, since that setting also independently drives id-generation cutoff logic in
    /// <see cref="PathSettings.GetPresentationUrl"/> - defaulting it there would be a much larger, unrelated
    /// behaviour change.
    /// </summary>
    private static readonly DateTimeOffset DefaultDeprecationDate = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    public async Task InvokeAsync(HttpContext context)
    {
        var settings = pathSettings.Value;
        var legacyHost = settings.LegacyPresentationApiUrl?.Host;

        if (string.IsNullOrEmpty(legacyHost))
        {
            logger.LogWarning(
                "LegacyHostRedirectMiddleware invoked with no configured legacy hostname");
            await next(context);
            return;
        }

        if (!string.Equals(context.Request.Host.Host, legacyHost, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Always the default host, never a customer-specific CustomerPresentationApiUrl override: those are
        // reverse-proxy hosts with their own independent path-rewriting rules in front of this API, not something
        // this app can redirect straight to a canonical path on - the proxy's rewritten path isn't guaranteed to
        // match the one generated here
        var targetHost = settings.PresentationApiUrl;

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

        // RFC 9745 requires "Deprecation" to be a Structured-Fields Date (RFC 9651 3.3.7 - "@" + seconds since the
        // Unix epoch) - there's no "unknown date" form to omit it with, so this always has a value: the configured
        // LegacyHostnameCutoffDate if set, else DefaultDeprecationDate
        var deprecationValue = ToStructuredFieldDate(settings.LegacyHostnameCutoffDate ?? DefaultDeprecationDate);
        var sunsetValue = settings.LegacyHostSunsetDate?.UtcDateTime.ToString("R");

        context.Response.OnStarting(state =>
        {
            var (response, location, deprecation, sunset) = ((HttpResponse, string, string, string?))state;
            response.Headers.Append("Deprecation", deprecation);
            response.Headers.Append("Link", $"<{location}>; rel=\"successor-version\"");
            if (!string.IsNullOrEmpty(sunset)) response.Headers.Append("Sunset", sunset);
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
    /// Formats as a Structured-Fields Date (RFC 9651 section 3.3.7 - "@" followed by signed seconds since the Unix
    /// epoch, e.g. "@1767225600") for the "Deprecation" response header - RFC 9745 requires this exact form.
    /// </summary>
    private static string ToStructuredFieldDate(DateTimeOffset value) => $"@{value.ToUnixTimeSeconds()}";

    private static bool IsMutatingMethod(string method) =>
        HttpMethods.IsPut(method) || HttpMethods.IsPost(method) || HttpMethods.IsDelete(method) ||
        HttpMethods.IsPatch(method);
}
