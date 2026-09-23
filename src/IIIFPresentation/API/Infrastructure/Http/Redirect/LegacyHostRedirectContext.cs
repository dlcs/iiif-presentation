namespace API.Infrastructure.Http.Redirect;

/// <summary>
/// Carries the legacy host/scheme a request actually arrived on through <see cref="HttpContext.Items"/> for the
/// duration of a <see cref="LegacyHostRedirectMiddleware"/> in-place request, so that any internal redirect a
/// downstream handler issues (see <see cref="API.Infrastructure.PresentationController.SeeOther"/>) can be put back
/// on that host rather than the canonical one the request is being processed against.
/// </summary>
public static class LegacyHostRedirectContext
{
    private static readonly object ItemsKey = new();

    private readonly record struct OriginalHost(HostString Host, string Scheme, string CanonicalHost);

    /// <summary>
    /// Records the host/scheme a request actually arrived on, and the canonical host it's being processed in place
    /// against, against <paramref name="context"/> - called once, by <see cref="LegacyHostRedirectMiddleware"/>,
    /// before it swaps <see cref="HttpRequest.Host"/> over to the canonical host for downstream processing.
    /// </summary>
    public static void Set(HttpContext context, HostString legacyHost, string legacyScheme, string canonicalHost) =>
        context.Items[ItemsKey] = new OriginalHost(legacyHost, legacyScheme, canonicalHost);

    /// <summary>
    /// If <paramref name="location"/> is an absolute url on the canonical host <paramref name="context"/> is being
    /// processed in place against, rewrites it back onto the legacy host the client actually connected to - keeping
    /// path/query/fragment as-is - so the hop stays same-origin and an Authorization header survives the client
    /// following it. Returns <paramref name="location"/> unchanged otherwise, including when this request never
    /// came in on a legacy host at all (nothing recorded via <see cref="Set"/>).
    /// </summary>
    public static string RewriteToLegacyHostIfNeeded(HttpContext context, string location)
    {
        if (context.Items[ItemsKey] is not OriginalHost originalHost) return location;
        if (!Uri.TryCreate(location, UriKind.Absolute, out var locationUri)) return location;
        if (!string.Equals(locationUri.Host, originalHost.CanonicalHost, StringComparison.OrdinalIgnoreCase))
        {
            return location;
        }

        var rewritten = new UriBuilder(locationUri)
        {
            Scheme = originalHost.Scheme,
            Host = originalHost.Host.Host,
            Port = originalHost.Host.Port ?? -1
        };
        return rewritten.Uri.AbsoluteUri;
    }
}
