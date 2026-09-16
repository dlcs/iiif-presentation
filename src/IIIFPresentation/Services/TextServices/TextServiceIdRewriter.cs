using IIIF;
using IIIF.Presentation.V3;
using IIIF.Search.V2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Paths;
using Services.Manifests.Settings;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.TextServices;

/// <summary>
/// Rewrites ids embedded in a text-services response (search/autocomplete services, rendering, manifest- and
/// canvas-level annotations) onto the customer-facing presentation host, re-templating the id-portion per link
/// category via PathRules. Kept separate from <see cref="ITextManifestAugmentor"/>'s merge logic - this is a path
/// rewrite concern, not manifest augmentation, matching how <see cref="IPathGenerator"/>/
/// <see cref="IPathRewriteParser"/> are kept separate elsewhere in this codebase.
/// </summary>
public interface ITextServiceIdRewriter
{
    /// <summary>
    /// Rewrites every id in <paramref name="augmented"/> (search/autocomplete services, rendering, manifest- and
    /// canvas-level annotations) in place, onto <paramref name="dbManifest"/>'s customer-facing presentation host.
    /// </summary>
    void Rewrite(Manifest augmented, DbManifest dbManifest, TextJobId jobId);
}

public class TextServiceIdRewriter(IOptions<PathSettings> pathOptions, ILogger<TextServiceIdRewriter> logger)
    : ITextServiceIdRewriter
{
    /// <summary>
    /// Rewrites every id text-services returned (search/autocomplete services, rendering, manifest- and
    /// canvas-level annotations) onto <paramref name="dbManifest"/>'s customer-facing presentation host,
    /// re-templating the id-portion per link category via PathRules (<see cref="PresentationResourceType.TextServiceSearchService"/>,
    /// <see cref="PresentationResourceType.TextServiceRendering"/>,
    /// <see cref="PresentationResourceType.TextServiceAnnotations"/> - each falling back to
    /// <see cref="PresentationResourceType.TextServiceJob"/>'s template when not explicitly configured).
    /// </summary>
    /// <remarks>
    /// text-services builds its search/rendering/annotation URLs from the X-Forwarded-Host/-Path we send (see
    /// <see cref="TextSearchClient"/>), but only honours them when that host is in *its own* server-side
    /// allowlist - so a config change on our side isn't guaranteed to take effect there. Rewriting ourselves means
    /// these links are correct regardless of that allowlist.
    /// </remarks>
    public void Rewrite(Manifest augmented, DbManifest dbManifest, TextJobId jobId)
    {
        var targetHost = pathOptions.Value.GetPresentationUrl(dbManifest.CustomerId, dbManifest.Created);

        // The literal id text-services embeds when it falls back to the plain job id (i.e. doesn't honour our
        // forwarded host/path at all - see the allowlist note above) - TextJobId's own ToString() shape.
        var rawId = jobId.ToString();

        // What TextServiceJob currently resolves to for this host - the "fallback" shape every category inherits
        // when it has no override of its own (see TypedPathTemplateOptions' FallbackTypes), and also what
        // text-services embeds if it *does* honour our forwarded X-Forwarded-Path, which is built from this same
        // template (see TextSearchClient.GetForwardedJobId). Searching for this - not just rawId - means a
        // category-specific override (e.g. TextServiceRendering) correctly overwrites whatever TextServiceJob's
        // own template currently produces for this host, purely from config, with no hardcoded assumptions here
        // about what that template looks like.
        var fallbackId = ResolveIdSuffix(PresentationResourceType.TextServiceJob, targetHost, jobId);

        var searchIdSuffix = ResolveIdSuffix(PresentationResourceType.TextServiceSearchService, targetHost, jobId);
        var renderingIdSuffix = ResolveIdSuffix(PresentationResourceType.TextServiceRendering, targetHost, jobId);
        var annotationsIdSuffix =
            ResolveIdSuffix(PresentationResourceType.TextServiceAnnotations, targetHost, jobId);

        var rewritten = 0;

        if (augmented.Service != null)
        {
            foreach (var searchService in augmented.Service.OfType<SearchService2>())
            {
                rewritten += RewriteId(searchService, rawId, fallbackId, searchIdSuffix, targetHost);
                if (searchService.Service == null) continue;
                foreach (var nested in searchService.Service)
                {
                    rewritten += RewriteId(nested, rawId, fallbackId, searchIdSuffix, targetHost);
                }
            }
        }

        if (augmented.Rendering != null)
        {
            foreach (var rendering in augmented.Rendering)
            {
                rewritten += RewriteId(rendering, rawId, fallbackId, renderingIdSuffix, targetHost);
            }
        }

        if (augmented.Annotations != null)
        {
            foreach (var annotation in augmented.Annotations)
            {
                rewritten += RewriteId(annotation, rawId, fallbackId, annotationsIdSuffix, targetHost);
            }
        }

        if (augmented.Items != null)
        {
            foreach (var canvas in augmented.Items)
            {
                if (canvas.Annotations == null) continue;
                foreach (var annotation in canvas.Annotations)
                {
                    rewritten += RewriteId(annotation, rawId, fallbackId, annotationsIdSuffix, targetHost);
                }
            }
        }

        logger.LogDebug("Rewrote {Count} text-services ids to {TargetHost} for job {JobId}", rewritten, targetHost,
            jobId);
    }

    /// <summary>
    /// Resolves the id-portion template for a link category, keyed by <paramref name="targetHost"/> (the same
    /// host these links are being rewritten onto), and renders it for this job.
    /// </summary>
    private string ResolveIdSuffix(string resourceType, Uri targetHost, TextJobId jobId)
    {
        var template = pathOptions.Value.PathRules.GetPathTemplateForHostAndType(targetHost.Host, resourceType);
        return template.GeneratePath(jobId.CustomerId, resourceId: jobId.ResourceId).TrimStart('/');
    }

    private static int RewriteId(IResource resource, string rawId, string fallbackId, string newIdSuffix,
        Uri targetHost)
    {
        if (string.IsNullOrEmpty(resource.Id)) return 0;

        // fallbackId (what TextServiceJob currently resolves to for this host, from PathRules config) is always
        // preferred over rawId (the bare, unconfigurable job id, used only when text-services doesn't honour our
        // forwarding at all) - rawId is a last-resort match, not an equal alternative.
        var rewrittenId = resource.Id.Contains(fallbackId, StringComparison.Ordinal)
            ? resource.Id.Replace(fallbackId, newIdSuffix, StringComparison.Ordinal)
            : resource.Id.Contains(rawId, StringComparison.Ordinal)
                ? resource.Id.Replace(rawId, newIdSuffix, StringComparison.Ordinal)
                : resource.Id;

        if (!Uri.TryCreate(rewrittenId, UriKind.Absolute, out var parsed)) return 0;

        resource.Id = new UriBuilder(parsed)
        {
            Scheme = targetHost.Scheme,
            Host = targetHost.Host,
            Port = targetHost.IsDefaultPort ? -1 : targetHost.Port,
        }.Uri.ToString();
        return 1;
    }
}
