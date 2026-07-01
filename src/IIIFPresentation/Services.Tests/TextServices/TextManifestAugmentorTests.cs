using FakeItEasy;
using IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V3;
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
        sut = new TextManifestAugmentor(textSearchClient, new NullLogger<TextManifestAugmentor>());
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