using DLCS;
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
/// canvas-level annotations) onto the DLCS orchestrator host, re-templating the id-portion per link category via
/// PathRules. Kept separate from <see cref="ITextManifestAugmentor"/>'s merge logic - this is a path rewrite
/// concern, not manifest augmentation, matching how <see cref="IPathGenerator"/>/
/// <see cref="IPathRewriteParser"/> are kept separate elsewhere in this codebase.
/// </summary>
public interface ITextServiceIdRewriter
{
    /// <summary>
    /// Rewrites every id in <paramref name="augmented"/> (search/autocomplete services, rendering, manifest- and
    /// canvas-level annotations) in place, onto <paramref name="dbManifest"/>'s DLCS orchestrator host.
    /// </summary>
    void Rewrite(Manifest augmented, DbManifest dbManifest, TextJobId jobId);
}

public class TextServiceIdRewriter(
    IOptions<PathSettings> pathOptions,
    IOptions<DlcsSettings> dlcsOptions,
    ILogger<TextServiceIdRewriter> logger)
    : ITextServiceIdRewriter
{
    /// <summary>
    /// Rewrites every id text-services returned (search/autocomplete services, rendering, manifest- and
    /// canvas-level annotations) onto <paramref name="dbManifest"/>'s DLCS orchestrator host, re-templating the
    /// id-portion per link category via PathRules (<see cref="PresentationResourceType.TextServiceSearchService"/>,
    /// <see cref="PresentationResourceType.TextServiceRendering"/>,
    /// <see cref="PresentationResourceType.TextServiceAnnotations"/> - each falling back to
    /// <see cref="PresentationResourceType.TextServiceJob"/>'s template when not explicitly configured). Only
    /// rewrites the id of each top-level resource in a container (e.g. an <see cref="AnnotationPage"/>'s own id) -
    /// text-services is not expected to nest further ids (e.g. per-annotation ids) inside those containers.
    /// </summary>
    /// <remarks>
    /// text-services builds its search/rendering/annotation URLs from the X-Forwarded-Host/-Path we send (see
    /// <see cref="TextSearchClient"/>), which is always the orchestrator host - these resources are served from
    /// there, not from the presentation host. text-services only honours our forwarded host/path when it's in
    /// *its own* server-side allowlist though, so a config change on our side isn't guaranteed to take effect
    /// there. Rewriting ourselves means these links are correct regardless of that allowlist.
    /// </remarks>
    public void Rewrite(Manifest augmented, DbManifest dbManifest, TextJobId jobId)
    {
        var targetHost = dlcsOptions.Value.GetOrchestratorUri(dbManifest.CustomerId);

        var jobIdString = jobId.ToString();

        var configuredJobId =
            ResolveDestination(PresentationResourceType.TextServiceJob, targetHost.Host, jobId).Suffix;

        // The destination shapes below are always resolved for the orchestrator host - that's where these
        // resources are actually served from, regardless of which host the manifest itself resolves to - unless
        // the configured template is itself an absolute URL, in which case it names its own destination host.
        var searchId = ResolveDestination(PresentationResourceType.TextServiceSearchService, targetHost.Host, jobId);
        var renderingId = ResolveDestination(PresentationResourceType.TextServiceRendering, targetHost.Host, jobId);
        var annotationsId =
            ResolveDestination(PresentationResourceType.TextServiceAnnotations, targetHost.Host, jobId);

        var rewritten = 0;

        if (augmented.Service != null)
        {
            foreach (var searchService in augmented.Service.OfType<SearchService2>())
            {
                rewritten += RewriteId(searchService, jobIdString, configuredJobId, searchId, targetHost);
                if (searchService.Service == null) continue;
                foreach (var nested in searchService.Service)
                {
                    rewritten += RewriteId(nested, jobIdString, configuredJobId, searchId, targetHost);
                }
            }
        }

        if (augmented.Rendering != null)
        {
            foreach (var rendering in augmented.Rendering)
            {
                rewritten += RewriteId(rendering, jobIdString, configuredJobId, renderingId, targetHost);
            }
        }

        if (augmented.Annotations != null)
        {
            foreach (var annotation in augmented.Annotations)
            {
                rewritten += RewriteId(annotation, jobIdString, configuredJobId, annotationsId, targetHost);
            }
        }

        if (augmented.Items != null)
        {
            foreach (var canvas in augmented.Items)
            {
                if (canvas.Annotations == null) continue;
                foreach (var annotation in canvas.Annotations)
                {
                    rewritten += RewriteId(annotation, jobIdString, configuredJobId, annotationsId, targetHost);
                }
            }
        }

        logger.LogDebug("Rewrote {Count} text-services ids to {TargetHost} for job {JobId}", rewritten, targetHost,
            jobId);
    }

    /// <summary>
    /// The id-portion template for a link category, resolved for this job. Normally just <see cref="Suffix"/> - a
    /// path to splice onto the target host in place of whatever job-id shape was matched (see RewriteId). If the
    /// configured template was itself an absolute URL though, it names the rewritten id's destination outright:
    /// <see cref="AbsoluteId"/> is that full id, used as-is instead of being spliced onto anything.
    /// </summary>
    private readonly record struct ResolvedId(string Suffix, Uri? AbsoluteId);

    /// <summary>
    /// Resolves the destination template for a link category, keyed by <paramref name="host"/>, and renders it for
    /// this job.
    /// </summary>
    private ResolvedId ResolveDestination(string resourceType, string host, TextJobId jobId)
    {
        var template = pathOptions.Value.PathRules.GetPathTemplateForHostAndType(host, resourceType);
        var generated = template.GeneratePath(jobId.CustomerId, resourceId: jobId.ResourceId);

        // A PathRules template may be configured as an absolute URL (as e.g. ResourcePublic can be, per
        // PathRewriteParser) - when it is, it names the rewritten id's destination outright (see RewriteId), not
        // just an id-portion to graft onto the target presentation host.
        if (Uri.TryCreate(generated, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return new ResolvedId(string.Empty, absolute);
        }

        return new ResolvedId(generated.TrimStart('/'), null);
    }

    private int RewriteId(IResource resource, string rawId, string configuredJobId, ResolvedId newId, Uri targetHost)
    {
        if (string.IsNullOrEmpty(resource.Id)) return 0;

        var matched = SelectMatchingSuffix(resource.Id, configuredJobId, rawId);
        if (matched == null)
        {
            logger.LogWarning(
                "Could not find a recognised job-id shape (expected suffix {ConfiguredJobId} or {RawId}) in " +
                "text-services id {Id} - leaving it unrewritten", configuredJobId, rawId, resource.Id);
            return 0;
        }

        // An absolute-URL template names the destination outright - use it as-is rather than splicing it onto
        // whatever text-services' original id looked like.
        if (newId.AbsoluteId != null)
        {
            resource.Id = newId.AbsoluteId.ToString();
            return 1;
        }

        // Remove everything that we've matched as not needed, and replace it with the new suffix
        var rewrittenId = resource.Id[..^matched.Length] + newId.Suffix;

        if (!Uri.TryCreate(rewrittenId, UriKind.Absolute, out var parsed))
        {
            logger.LogWarning(
                "Rewritten text-services id {RewrittenId} is not a valid absolute URI - leaving {Id} unrewritten",
                rewrittenId, resource.Id);
            return 0;
        }

        resource.Id = new UriBuilder(parsed)
        {
            Scheme = targetHost.Scheme,
            Host = targetHost.Host,
            Port = targetHost.IsDefaultPort ? -1 : targetHost.Port,
        }.Uri.ToString();
        return 1;
    }

    /// <summary>
    /// Picks whichever of <paramref name="configuredJobId"/>/<paramref name="jobId"/> is present as a
    /// path-segment-anchored suffix of <paramref name="id"/>
    /// </summary>
    private static string? SelectMatchingSuffix(string id, string configuredJobId, string jobId)
    {
        var configuredMatches = EndsWithSegment(id, configuredJobId);
        var rawMatches = EndsWithSegment(id, jobId);

        // Both can match when one is a suffix of the other (e.g. configuredJobId "my-manifest" within jobId
        // "1/iiif/my-manifest") - picking the shorter one there would leave the longer one's extra prefix behind,
        // orphaned, in the rewritten id, so the longer (more complete) match always wins.
        if (configuredMatches && rawMatches) return configuredJobId.Length >= jobId.Length ? configuredJobId : jobId;
        if (configuredMatches) return configuredJobId;
        return rawMatches ? jobId : null;
    }

    private static bool EndsWithSegment(string id, string candidate)
    {
        if (candidate.Length == 0 || !id.EndsWith(candidate, StringComparison.Ordinal)) return false;
        var boundaryIndex = id.Length - candidate.Length - 1;
        return boundaryIndex < 0 || id[boundaryIndex] == '/';
    }
}
