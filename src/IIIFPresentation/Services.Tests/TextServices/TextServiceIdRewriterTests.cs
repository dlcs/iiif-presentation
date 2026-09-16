using Core.Paths;
using Core.Web;
using DLCS;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Search.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DbManifest = Models.Database.Collections.Manifest;
using Services.Manifests.Settings;
using Services.TextServices;

namespace Services.Tests.TextServices;

public class TextServiceIdRewriterTests
{
    private static readonly DbManifest DbManifest = new() { Id = "my-manifest", CustomerId = 1 };

    // DbManifest here has CustomerId=1, Id="my-manifest" - TextJobId.ToString() is "1/iiif/my-manifest", the raw
    // id text-services embeds when it doesn't honour our forwarded host/path.
    private const string RawId = "1/iiif/my-manifest";
    private static readonly TextJobId JobId = new(DbManifest.CustomerId, DbManifest.Id);

    // Defaults the orchestrator host (used to build the X-Forwarded-Path text-services actually honours - see
    // TextSearchClient) to the same host as the presentation host being rewritten onto, so most tests here don't
    // need to think about the two separately - Rewrite_ResolvesFallbackId_FromOrchestratorHost_NotPresentationHost
    // below is the one that deliberately makes them differ.
    private static TextServiceIdRewriter CreateSut(PathSettings pathSettings, DlcsSettings? dlcsSettings = null) =>
        new(Options.Create(pathSettings),
            Options.Create(dlcsSettings ?? new DlcsSettings
            {
                ApiUri = new Uri("https://dlcs.example"), OrchestratorUri = pathSettings.PresentationApiUrl
            }),
            new NullLogger<TextServiceIdRewriter>());

    [Fact]
    public void Rewrite_ChangesHost_ToConfiguredCustomerPresentationHost()
    {
        var searchService = new SearchService2 { Id = $"https://text-services.internal/search/v2/{RawId}" };
        var augmented = new Manifest { Service = [searchService] };

        // No PathRules overrides configured - id-portion keeps the default TextServiceJob shape, only the host
        // should change.
        var sut = CreateSut(new PathSettings { PresentationApiUrl = new Uri("https://rewritten.example") });

        sut.Rewrite(augmented, DbManifest, JobId);

        searchService.Id.Should().Be($"https://rewritten.example/search/v2/{RawId}");
    }

    [Fact]
    public void Rewrite_RewritesNestedAutocompleteService()
    {
        var autoComplete = new AutoCompleteService2 { Id = $"https://text-services.internal/autocomplete/v2/{RawId}" };
        var searchService = new SearchService2
        {
            Id = $"https://text-services.internal/search/v2/{RawId}",
            Service = [autoComplete]
        };
        var augmented = new Manifest { Service = [searchService] };

        var sut = CreateSut(new PathSettings { PresentationApiUrl = new Uri("https://rewritten.example") });

        sut.Rewrite(augmented, DbManifest, JobId);

        autoComplete.Id.Should().Be($"https://rewritten.example/autocomplete/v2/{RawId}");
    }

    [Fact]
    public void Rewrite_RewritesRenderingId_UsingConfiguredTextServiceRenderingTemplate()
    {
        var pdfRendering = new ExternalResource("Text") { Id = $"https://text-services.internal/pdf/v1/{RawId}" };
        var augmented = new Manifest { Rendering = [pdfRendering] };

        var sut = CreateSut(new PathSettings
        {
            PresentationApiUrl = new Uri("https://rewritten.example"),
            PathRules = new TypedPathTemplateOptions
            {
                Overrides = new Dictionary<string, Dictionary<string, PathTemplate>>
                {
                    ["rewritten.example"] = new() { ["TextServiceRendering"] = "/{customerId}/pdf/{resourceId}" }
                }
            }
        });

        sut.Rewrite(augmented, DbManifest, JobId);

        pdfRendering.Id.Should().Be("https://rewritten.example/pdf/v1/1/pdf/my-manifest");
    }

    [Fact]
    public void Rewrite_RewritesManifestAndCanvasAnnotationIds_UsingConfiguredTextServiceAnnotationsTemplate()
    {
        var manifestAnnotations = new AnnotationPage
            { Id = $"https://text-services.internal/annotations/manifest/v1/{RawId}" };
        var canvasAnnotations = new AnnotationPage
            { Id = $"https://text-services.internal/annotations/lines/v1/0/{RawId}" };
        var augmented = new Manifest
        {
            Annotations = [manifestAnnotations],
            Items = [new Canvas { Id = "https://example.com/canvas/1", Annotations = [canvasAnnotations] }]
        };

        var sut = CreateSut(new PathSettings
        {
            PresentationApiUrl = new Uri("https://rewritten.example"),
            PathRules = new TypedPathTemplateOptions
            {
                Overrides = new Dictionary<string, Dictionary<string, PathTemplate>>
                {
                    ["rewritten.example"] = new() { ["TextServiceAnnotations"] = "/{customerId}/annotations/{resourceId}" }
                }
            }
        });

        sut.Rewrite(augmented, DbManifest, JobId);

        manifestAnnotations.Id.Should()
            .Be("https://rewritten.example/annotations/manifest/v1/1/annotations/my-manifest");
        canvasAnnotations.Id.Should()
            .Be("https://rewritten.example/annotations/lines/v1/0/1/annotations/my-manifest");
    }

    [Fact]
    public void Rewrite_FallsBackToHostsTextServiceJobOverride_ForAnnotations_WhenNotExplicitlyConfigured()
    {
        // text-services honours our forwarding for this host, so the id it embeds is already in the shape
        // TextServiceJob's override produces for this host ("my-manifest", from "/{resourceId}") - not the raw
        // default job-id shape.
        var annotationPage = new AnnotationPage
            { Id = "https://text-services.internal/annotations/manifest/v1/my-manifest" };
        var augmented = new Manifest { Annotations = [annotationPage] };

        // Only TextServiceJob is overridden for this host - TextServiceAnnotations has no override of its own,
        // so it should fall back to this host's TextServiceJob override, not the global default.
        var sut = CreateSut(new PathSettings
        {
            PresentationApiUrl = new Uri("https://rewritten.example"),
            PathRules = new TypedPathTemplateOptions
            {
                Overrides = new Dictionary<string, Dictionary<string, PathTemplate>>
                {
                    ["rewritten.example"] = new() { ["TextServiceJob"] = "/{resourceId}" }
                }
            }
        });

        sut.Rewrite(augmented, DbManifest, JobId);

        annotationPage.Id.Should().Be("https://rewritten.example/annotations/manifest/v1/my-manifest");
    }

    [Fact]
    public void Rewrite_UsesCategoryOverride_WhenItDiffersFromTextServiceJobsResolvedValue()
    {
        // Reproduces a real observed shape: TextServiceJob is configured with an extra path segment baked in for
        // this host (correct for search, which relies on it), so text-services embeds that shape verbatim into
        // every id it returns, including rendering. TextServiceRendering has its own, different override for the
        // same host - the rewriter should detect TextServiceJob's *current* resolved value (not a hardcoded
        // string) in the returned id and swap it for TextServiceRendering's, purely from config.
        var pdfRendering = new ExternalResource("Text")
            { Id = $"https://text-services.internal/pdf/v1/extra-segment/{RawId}" };
        var augmented = new Manifest { Rendering = [pdfRendering] };

        var sut = CreateSut(new PathSettings
        {
            PresentationApiUrl = new Uri("https://rewritten.example"),
            PathRules = new TypedPathTemplateOptions
            {
                Overrides = new Dictionary<string, Dictionary<string, PathTemplate>>
                {
                    ["rewritten.example"] = new()
                    {
                        ["TextServiceJob"] = "/extra-segment/{customerId}/iiif/{resourceId}",
                        ["TextServiceRendering"] = "/{customerId}/iiif/{resourceId}",
                    }
                }
            }
        });

        sut.Rewrite(augmented, DbManifest, JobId);

        pdfRendering.Id.Should().Be($"https://rewritten.example/pdf/v1/{RawId}",
            "TextServiceRendering's own override replaces whatever TextServiceJob's template currently resolves to");
    }

    [Fact]
    public void Rewrite_ResolvesFallbackId_FromOrchestratorHost_NotPresentationHost()
    {
        // TextServiceJob is overridden for the DLCS orchestrator host - the host TextSearchClient actually builds
        // the X-Forwarded-Path from (see TextSearchClient.GetForwardedJobId) - not for the customer-facing
        // presentation host this rewriter targets. text-services embeds ids shaped by the orchestrator host's
        // template, so that - not the presentation host's (here, unconfigured/default) template - is what the
        // rewriter needs to search for.
        var annotationPage = new AnnotationPage
            { Id = "https://text-services.internal/annotations/manifest/v1/my-manifest" };
        var augmented = new Manifest { Annotations = [annotationPage] };

        var sut = CreateSut(
            new PathSettings
            {
                PresentationApiUrl = new Uri("https://rewritten.example"),
                PathRules = new TypedPathTemplateOptions
                {
                    Overrides = new Dictionary<string, Dictionary<string, PathTemplate>>
                    {
                        ["orchestrator.example"] = new() { ["TextServiceJob"] = "/{resourceId}" }
                    }
                }
            },
            new DlcsSettings
            {
                ApiUri = new Uri("https://dlcs.example"), OrchestratorUri = new Uri("https://orchestrator.example")
            });

        sut.Rewrite(augmented, DbManifest, JobId);

        annotationPage.Id.Should().Be("https://rewritten.example/annotations/manifest/v1/1/iiif/my-manifest",
            "the orchestrator host's TextServiceJob override is what text-services actually used to shape this " +
            "id, even though the presentation host (with no override) resolves TextServiceJob differently");
    }

    [Fact]
    public void Rewrite_PrefersLongerAnchoredMatch_WhenFallbackIdIsASuffixOfRawId()
    {
        // text-services did not honour our forwarding for this host, so the id it returned is in the raw/default
        // job-id shape - but this host's TextServiceJob is also overridden to something short enough to be a
        // path-segment-anchored suffix of that same raw shape ("my-manifest" is the tail of "1/iiif/my-manifest").
        // The rewriter must match and replace the full raw shape, not just the shorter overlapping suffix, or it
        // leaves an orphaned "1/iiif/" prefix behind in the output.
        var annotationPage = new AnnotationPage { Id = $"https://text-services.internal/annotations/manifest/v1/{RawId}" };
        var augmented = new Manifest { Annotations = [annotationPage] };

        var sut = CreateSut(new PathSettings
        {
            PresentationApiUrl = new Uri("https://rewritten.example"),
            PathRules = new TypedPathTemplateOptions
            {
                Overrides = new Dictionary<string, Dictionary<string, PathTemplate>>
                {
                    ["rewritten.example"] = new() { ["TextServiceJob"] = "/{resourceId}" }
                }
            }
        });

        sut.Rewrite(augmented, DbManifest, JobId);

        annotationPage.Id.Should().Be("https://rewritten.example/annotations/manifest/v1/my-manifest",
            "the full raw job-id shape is matched and replaced wholesale, not just the short TextServiceJob-shaped " +
            "suffix within it, which would leave '1/iiif/' orphaned in the output");
    }

    [Fact]
    public void Rewrite_UsesTheConfiguredTemplatesOwnHost_WhenTemplateIsAnAbsoluteUrl()
    {
        // PathRules templates can be configured as absolute URLs elsewhere (see PathRewriteParser) - when one is,
        // that host is the destination for the rewritten id, replacing the target presentation host entirely
        // (rather than the presentation host's scheme/host being kept and the configured host getting spliced into
        // the path, which would produce a broken, nonsensical id).
        var pdfRendering = new ExternalResource("Text") { Id = $"https://text-services.internal/pdf/v1/{RawId}" };
        var augmented = new Manifest { Rendering = [pdfRendering] };

        var sut = CreateSut(new PathSettings
        {
            PresentationApiUrl = new Uri("https://rewritten.example"),
            PathRules = new TypedPathTemplateOptions
            {
                Overrides = new Dictionary<string, Dictionary<string, PathTemplate>>
                {
                    ["rewritten.example"] = new()
                    {
                        ["TextServiceRendering"] = "https://configured-fqdn.example/{customerId}/pdf/{resourceId}"
                    }
                }
            }
        });

        sut.Rewrite(augmented, DbManifest, JobId);

        pdfRendering.Id.Should().Be("https://configured-fqdn.example/1/pdf/my-manifest");
    }

    [Fact]
    public void Rewrite_LeavesIdUntouched_WhenNoRecognisedJobIdShapeFound()
    {
        // An id that doesn't contain either candidate job-id shape at all (e.g. because text-services used some
        // completely different, unconfigured host/path) should be left exactly as-is - not partially rewritten by
        // swapping only the host and leaving the wrong path behind.
        var annotationPage = new AnnotationPage
            { Id = "https://text-services.internal/annotations/manifest/v1/completely-unrelated-id" };
        var augmented = new Manifest { Annotations = [annotationPage] };

        var sut = CreateSut(new PathSettings { PresentationApiUrl = new Uri("https://rewritten.example") });

        sut.Rewrite(augmented, DbManifest, JobId);

        annotationPage.Id.Should().Be("https://text-services.internal/annotations/manifest/v1/completely-unrelated-id",
            "with no recognised job-id shape to replace, the id is left untouched rather than partially rewritten");
    }

    [Fact]
    public void Rewrite_DoesNothing_WhenNoRelevantContentPresent()
    {
        var augmented = new Manifest { Id = "https://text-services.internal/text-augmented/v3/1/iiif/my-manifest" };

        var sut = CreateSut(new PathSettings { PresentationApiUrl = new Uri("https://rewritten.example") });

        sut.Rewrite(augmented, DbManifest, JobId);

        // The manifest's own top-level id is not one of the categories the rewriter touches
        augmented.Id.Should().Be("https://text-services.internal/text-augmented/v3/1/iiif/my-manifest");
    }
}
