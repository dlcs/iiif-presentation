using System.Net;
using Core.Settings;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Services.TextServices;

namespace Services.Tests.TextServices;

public class TextSearchClientTests
{
    private readonly TestMessageHandler messageHandler = new();

    private TextSearchClient CreateSut(TextServicesSettings settings) =>
        new(new HttpClient(messageHandler), Options.Create(settings), new NullLogger<TextSearchClient>());

    [Fact]
    public async Task GetTextAugmentedManifest_ReturnsNull_WhenSearchApiUriNotConfigured()
    {
        var sut = CreateSut(new TextServicesSettings { SearchApiUri = null });

        var result = await sut.GetTextAugmentedManifest("1/iiif/my-manifest", CancellationToken.None);

        result.Should().BeNull();
        messageHandler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTextAugmentedManifest_ReturnsNull_When404()
    {
        var sut = CreateSut(new TextServicesSettings { SearchApiUri = new Uri("http://search-api/") });
        messageHandler.Enqueue(HttpStatusCode.NotFound);

        var result = await sut.GetTextAugmentedManifest("1/iiif/my-manifest", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTextAugmentedManifest_ReturnsDeserializedManifest_WhenSuccessful()
    {
        var sut = CreateSut(new TextServicesSettings { SearchApiUri = new Uri("http://search-api/") });
        var manifest = new Manifest { Id = "https://example.com/manifest" };
        messageHandler.Enqueue(HttpStatusCode.OK, manifest.AsJson());

        var result = await sut.GetTextAugmentedManifest("1/iiif/my-manifest", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be("https://example.com/manifest");
    }

    [Fact]
    public async Task GetTextAugmentedManifest_SetsForwardedHeaders_WhenConfigured()
    {
        var sut = CreateSut(new TextServicesSettings
        {
            SearchApiUri = new Uri("http://search-api/"),
            CustomerOrchestratorUri = "orchestrator.example.com",
            PathRules = "/path/rules"
        });
        messageHandler.Enqueue(HttpStatusCode.OK, new Manifest().AsJson());

        await sut.GetTextAugmentedManifest("1/iiif/my-manifest", CancellationToken.None);

        var request = messageHandler.Requests.Single();
        request.Headers.GetValues("X-Forwarded-Host").Single().Should().Be("orchestrator.example.com");
        request.Headers.GetValues("X-Forwarded-Path").Single().Should().Be("/path/rules");
    }
}
