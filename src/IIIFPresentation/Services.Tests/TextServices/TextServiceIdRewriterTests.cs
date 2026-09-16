using Core.Paths;
using Core.Web;
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

    private static TextServiceIdRewriter CreateSut(PathSettings pathSettings) =>
        new(Options.Create(pathSettings), new NullLogger<TextServiceIdRewriter>());

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
        var annotationPage = new AnnotationPage
            { Id = $"https://text-services.internal/annotations/manifest/v1/{RawId}" };
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
    public void Rewrite_DoesNothing_WhenNoRelevantContentPresent()
    {
        var augmented = new Manifest { Id = "https://text-services.internal/text-augmented/v3/1/iiif/my-manifest" };

        var sut = CreateSut(new PathSettings { PresentationApiUrl = new Uri("https://rewritten.example") });

        sut.Rewrite(augmented, DbManifest, JobId);

        // The manifest's own top-level id is not one of the categories the rewriter touches
        augmented.Id.Should().Be("https://text-services.internal/text-augmented/v3/1/iiif/my-manifest");
    }
}
