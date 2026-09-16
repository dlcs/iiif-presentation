using FakeItEasy;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Search.V2;
using Microsoft.Extensions.Logging.Abstractions;
using DbManifest = Models.Database.Collections.Manifest;
using Newtonsoft.Json.Linq;
using Services.TextServices;

namespace Services.Tests.TextServices;

public class TextManifestAugmentorTests
{
    private readonly ITextSearchClient textSearchClient = A.Fake<ITextSearchClient>();
    private readonly TextManifestAugmentor sut;

    private static readonly DbManifest DbManifest = new() { Id = "my-manifest", CustomerId = 1 };

    public TextManifestAugmentorTests()
    {
        // Id rewriting is ITextServiceIdRewriter's own concern, with its own dedicated tests - use a no-op fake
        // here so these tests can assert on ids exactly as text-services returned them.
        var idRewriter = A.Fake<ITextServiceIdRewriter>();
        sut = new TextManifestAugmentor(textSearchClient, idRewriter, new NullLogger<TextManifestAugmentor>());
    }

    [Fact]
    public async Task Augment_ReturnsUnchangedManifest_WhenTextClientReturnsNull()
    {
        A.CallTo(() => textSearchClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Manifest?>(null));

        var manifest = new Manifest { Id = "https://example.com/manifest" };

        var result = await sut.Augment(manifest, DbManifest, CancellationToken.None);

        result.Should().BeSameAs(manifest);
        result.Service.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Augment_ReturnsUnchangedManifest_WhenAugmentedManifestHasNoSearchService()
    {
        var augmented = new Manifest { Id = "https://example.com/augmented" };
        A.CallTo(() => textSearchClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Manifest?>(augmented));

        var manifest = new Manifest { Id = "https://example.com/manifest" };

        var result = await sut.Augment(manifest, DbManifest, CancellationToken.None);

        result.Should().BeSameAs(manifest);
        result.Service.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Augment_AddsRendering_ToManifest()
    {
        var pdfRendering = new ExternalResource("Text") { Id = "https://example.com/pdf/v1" };
        var augmented = new Manifest { Rendering = [pdfRendering] };
        A.CallTo(() => textSearchClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Manifest?>(augmented));

        var manifest = new Manifest { Id = "https://example.com/manifest" };

        var result = await sut.Augment(manifest, DbManifest, CancellationToken.None);

        result.Rendering.Should().ContainSingle(r => r.Id == "https://example.com/pdf/v1");
    }

    [Fact]
    public async Task Augment_DoesNotDuplicateRendering_WhenAlreadyPresentOnManifest()
    {
        var pdfRendering = new ExternalResource("Text") { Id = "https://example.com/pdf/v1" };
        var augmented = new Manifest { Rendering = [pdfRendering] };
        A.CallTo(() => textSearchClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Manifest?>(augmented));

        var manifest = new Manifest
        {
            Id = "https://example.com/manifest",
            Rendering = [new ExternalResource("Text") { Id = "https://example.com/pdf/v1" }]
        };

        var result = await sut.Augment(manifest, DbManifest, CancellationToken.None);

        result.Rendering.Should().ContainSingle();
    }

    [Fact]
    public async Task Augment_AddsAnnotations_ToManifest()
    {
        var annotationPage = new AnnotationPage { Id = "https://example.com/annotations/1/v1" };
        var augmented = new Manifest { Annotations = [annotationPage] };
        A.CallTo(() => textSearchClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Manifest?>(augmented));

        var manifest = new Manifest { Id = "https://example.com/manifest" };

        var result = await sut.Augment(manifest, DbManifest, CancellationToken.None);

        result.Annotations.Should().ContainSingle(a => a.Id == "https://example.com/annotations/1/v1");
    }

    [Fact]
    public async Task Augment_AddsCanvasAnnotations_ToMatchingCanvas()
    {
        var linesRef = new AnnotationPage { Id = "https://example.com/annotations/lines/0/v1" };
        var wordsRef = new AnnotationPage { Id = "https://example.com/annotations/words/0/v1" };
        var augmented = new Manifest
        {
            Items = [new Canvas { Id = "https://example.com/canvas/1", Annotations = [linesRef, wordsRef] }]
        };
        A.CallTo(() => textSearchClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Manifest?>(augmented));

        var manifest = new Manifest
        {
            Id = "https://example.com/manifest",
            Items = [new Canvas { Id = "https://example.com/canvas/1" }]
        };

        var result = await sut.Augment(manifest, DbManifest, CancellationToken.None);

        result.Items!.Single().Annotations.Should().BeEquivalentTo([linesRef, wordsRef]);
    }

    [Fact]
    public async Task Augment_DoesNotAddCanvasAnnotations_WhenNoCanvasIdMatches()
    {
        var linesRef = new AnnotationPage { Id = "https://example.com/annotations/lines/0/v1" };
        var augmented = new Manifest
        {
            Items = [new Canvas { Id = "https://example.com/canvas/other", Annotations = [linesRef] }]
        };
        A.CallTo(() => textSearchClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Manifest?>(augmented));

        var manifest = new Manifest
        {
            Id = "https://example.com/manifest",
            Items = [new Canvas { Id = "https://example.com/canvas/1" }]
        };

        var result = await sut.Augment(manifest, DbManifest, CancellationToken.None);

        result.Items!.Single().Annotations.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Augment_AddsSearchServiceRenderingAndAnnotations_WhenAllPresent()
    {
        var searchService = new SearchService2 { Id = "https://example.com/search" };
        var pdfRendering = new ExternalResource("Text") { Id = "https://example.com/pdf/v1" };
        var annotationPage = new AnnotationPage { Id = "https://example.com/annotations/1/v1" };
        var augmented = new Manifest
        {
            Service = [searchService],
            Rendering = [pdfRendering],
            Annotations = [annotationPage]
        };
        A.CallTo(() => textSearchClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Manifest?>(augmented));

        var manifest = new Manifest { Id = "https://example.com/manifest" };
        manifest.EnsurePresentation3Context();

        var result = await sut.Augment(manifest, DbManifest, CancellationToken.None);

        result.Service.Should().ContainSingle(s => s.Id == "https://example.com/search");
        result.Rendering.Should().ContainSingle(r => r.Id == "https://example.com/pdf/v1");
        result.Annotations.Should().ContainSingle(a => a.Id == "https://example.com/annotations/1/v1");
    }

    [Fact]
    public async Task Augment_AddsSearchServiceAndContext_ToManifest()
    {
        var searchService = new SearchService2 { Id = "https://example.com/search" };
        var augmented = new Manifest { Service = [searchService] };
        A.CallTo(() => textSearchClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Manifest?>(augmented));

        var manifest = new Manifest { Id = "https://example.com/manifest" };
        manifest.EnsurePresentation3Context();

        var result = await sut.Augment(manifest, DbManifest, CancellationToken.None);

        result.Service.Should().ContainSingle(s => s.Id == "https://example.com/search");
        (result.Context as List<string>).Should().Contain(SearchService2.Search2Context);
    }

    [Fact]
    public async Task Augment_AddsSearchContext_WhenManifestContextIsJArray()
    {
        // Simulates a manifest whose Context was set as a JArray after JSON deserialisation
        var searchService = new SearchService2 { Id = "https://example.com/search" };
        var augmented = new Manifest { Service = [searchService] };
        A.CallTo(() => textSearchClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Manifest?>(augmented));

        var manifest = new Manifest
        {
            Id = "https://example.com/manifest",
            Context = new JArray { Context.Presentation3Context }
        };

        var result = await sut.Augment(manifest, DbManifest, CancellationToken.None);

        (result.Context as List<string>).Should().Contain(SearchService2.Search2Context);
    }

    [Fact]
    public async Task Augment_AddsSearchContext_WhenManifestContextIsJValueString()
    {
        // Simulates a manifest whose Context was set as a JValue string after JSON deserialisation
        var searchService = new SearchService2 { Id = "https://example.com/search" };
        var augmented = new Manifest { Service = [searchService] };
        A.CallTo(() => textSearchClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Manifest?>(augmented));

        var manifest = new Manifest
        {
            Id = "https://example.com/manifest",
            Context = new JValue(Context.Presentation3Context)
        };

        var result = await sut.Augment(manifest, DbManifest, CancellationToken.None);

        (result.Context as List<string>).Should().Contain(SearchService2.Search2Context);
    }

    [Fact]
    public async Task Augment_SetsDefaultLabels_OnSearchAndAutoCompleteServices()
    {
        var autoComplete = new AutoCompleteService2 { Id = "https://example.com/autocomplete" };
        var searchService = new SearchService2
        {
            Id = "https://example.com/search",
            Service = [autoComplete]
        };
        var augmented = new Manifest { Service = [searchService] };
        A.CallTo(() => textSearchClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Manifest?>(augmented));

        var manifest = new Manifest { Id = "https://example.com/manifest" };
        manifest.EnsurePresentation3Context();

        var result = await sut.Augment(manifest, DbManifest, CancellationToken.None);

        var addedSearch = result.Service!.OfType<SearchService2>().Single();
        addedSearch.Label!["en"].Single().Should().Be("Search within this manifest");
        addedSearch.Service!.OfType<AutoCompleteService2>().Single()
            .Label!["en"].Single().Should().Be("Autocomplete words in this manifest");
    }
}
