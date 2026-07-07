using System.Net;
using Core.Paths;
using Core.Settings;
using Core.Web;
using DLCS;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Services.TextServices;

namespace Services.Tests.TextServices;

public class TextSearchClientTests
{
    private static readonly TextJobId TestJobId = new(1, "my-manifest");

    private readonly TestMessageHandler messageHandler = new();

    private TextSearchClient CreateSut(TextServicesSettings? textServices = null,
        TypedPathTemplateOptions? typedPathTemplateOptions = null,
        DlcsSettings? dlcsOptions = null) =>
        new(new HttpClient(messageHandler),
            Options.Create(textServices ?? new TextServicesSettings { SearchApiUri = new Uri("http://search-api/") }),
            Options.Create(typedPathTemplateOptions ?? new TypedPathTemplateOptions()),
            Options.Create(dlcsOptions ?? new DlcsSettings
            {
                ApiUri = new Uri("https://dlcs.api"), OrchestratorUri = new Uri("https://orchestrator.example.com")
            }),
            new NullLogger<TextSearchClient>());

    [Fact]
    public async Task GetTextAugmentedManifest_ReturnsNull_WhenSearchApiUriNotConfigured()
    {
        var sut = CreateSut(new TextServicesSettings { SearchApiUri = null });

        var result = await sut.GetTextAugmentedManifest(TestJobId, CancellationToken.None);

        result.Should().BeNull();
        messageHandler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTextAugmentedManifest_ReturnsNull_When404()
    {
        var sut = CreateSut();
        messageHandler.Enqueue(HttpStatusCode.NotFound);

        var result = await sut.GetTextAugmentedManifest(TestJobId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTextAugmentedManifest_ReturnsDeserializedManifest_WhenSuccessful()
    {
        var sut = CreateSut();
        var manifest = new Manifest { Id = "https://example.com/manifest" };
        messageHandler.Enqueue(HttpStatusCode.OK, manifest.AsJson());

        var result = await sut.GetTextAugmentedManifest(TestJobId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be("https://example.com/manifest");
    }

    [Fact]
    public async Task GetTextAugmentedManifest_SetsForwardedHeaders_WithDefaults()
    {
        var sut = CreateSut();
        messageHandler.Enqueue(HttpStatusCode.OK, new Manifest().AsJson());

        await sut.GetTextAugmentedManifest(TestJobId, CancellationToken.None);

        var request = messageHandler.Requests.Single();
        request.Headers.GetValues("X-Forwarded-Host").Single().Should().Be("orchestrator.example.com");
        request.Headers.GetValues("X-Forwarded-Path").Single().Should().Be("/text-augmented/v3/1/iiif/my-manifest");
    }
    
    [Fact]
    public async Task GetTextAugmentedManifest_SetsForwardedHeaders_WithOverrides()
    {
        // Setup this user to use "orchestrator.diff" as Orchestrator host.
        // Setup a custom TextServicesJob template for that host.
        var sut = CreateSut(
            typedPathTemplateOptions: new TypedPathTemplateOptions
            {
                Overrides = new Dictionary<string, Dictionary<string, PathTemplate>>
                {
                    ["orchestrator.diff"] = new() { ["TextServiceJob"] = "/{resourceId}" }
                }
            },
            dlcsOptions: new DlcsSettings
            {
                ApiUri = new Uri("https://dlcs.api"),
                OrchestratorUri = new Uri("https://orchestrator.example.com"),
                CustomerOrchestratorUri = new Dictionary<int, Uri> { [1] = new("https://orchestrator.diff") }
            }
        );
        messageHandler.Enqueue(HttpStatusCode.OK, new Manifest().AsJson());

        await sut.GetTextAugmentedManifest(TestJobId, CancellationToken.None);

        var request = messageHandler.Requests.Single();
        request.Headers.GetValues("X-Forwarded-Host").Single().Should()
            .Be("orchestrator.diff", "Customer specific value used");
        request.Headers.GetValues("X-Forwarded-Path").Single().Should()
            .Be("/text-augmented/v3/my-manifest", "Domain specific value used");
    }
}
