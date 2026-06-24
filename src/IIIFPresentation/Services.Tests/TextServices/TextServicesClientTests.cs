using System.Net;
using System.Text;
using Core.Settings;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Database.General;
using Services.TextServices;

namespace Services.Tests.TextServices;

public class TextServicesClientTests
{
    private readonly TestMessageHandler messageHandler = new();

    private TextServicesClient CreateSut(TextServicesSettings settings) =>
        new(new HttpClient(messageHandler), Options.Create(settings), new NullLogger<TextServicesClient>());

    private static PipelineJob MakeJob(int customerId = 1, string resourceId = "my-manifest") =>
        new() { CustomerId = customerId, ManifestId = resourceId, JobType = PipelineJobType.TextService };

    [Fact]
    public async Task CreateOrUpdateJob_ReturnsTrue_WhenPostSucceeds()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.OK);

        var result = await sut.CreateOrUpdateJob(MakeJob(), "my-bucket", "staging/1/manifests/my-manifest");

        result.Should().BeTrue();
        messageHandler.Requests.Single().Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task CreateOrUpdateJob_FallsBackToPut_WhenPostReturns409()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.Conflict);
        messageHandler.Enqueue(HttpStatusCode.OK);

        var result = await sut.CreateOrUpdateJob(MakeJob(), "my-bucket", "staging/1/manifests/my-manifest");

        result.Should().BeTrue();
        messageHandler.Requests.Should().HaveCount(2);
        messageHandler.Requests[0].Method.Should().Be(HttpMethod.Post);
        messageHandler.Requests[1].Method.Should().Be(HttpMethod.Put);
    }

    [Fact]
    public async Task CreateOrUpdateJob_ReturnsFalse_WhenBuilderApiUriNotConfigured()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = null });

        var result = await sut.CreateOrUpdateJob(MakeJob(), "my-bucket", "staging/1/manifests/my-manifest");

        result.Should().BeFalse();
        messageHandler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateOrUpdateJob_ReturnsFalse_WhenPostReturnsNonSuccess()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.InternalServerError);

        var result = await sut.CreateOrUpdateJob(MakeJob(), "my-bucket", "staging/1/manifests/my-manifest");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CreateOrUpdateJob_SendsCorrectJobIdAndS3Uri_InPostBody()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.OK);

        await sut.CreateOrUpdateJob(MakeJob(customerId: 5, resourceId: "test-manifest"), "my-bucket",
            "staging/5/manifests/test-manifest");

        var body = await messageHandler.Requests.Single().Content!.ReadAsStringAsync();
        body.Should().Contain("\"id\":\"5/iiif/test-manifest\"");
        body.Should().Contain("\"sourceUri\":\"s3://my-bucket/staging/5/manifests/test-manifest\"");
    }

    [Fact]
    public async Task CreateOrUpdateJob_SendsSearchAutocompleteTextAugmented_AsServicesField()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.OK);

        await sut.CreateOrUpdateJob(MakeJob(), "my-bucket", "staging/1/manifests/my-manifest");

        var body = await messageHandler.Requests.Single().Content!.ReadAsStringAsync();
        var expected = (int)(JobServices.All);
        body.Should().Contain($"\"services\":{expected}");
    }

    [Fact]
    public async Task GetTextAugmentedManifest_ReturnsNull_WhenSearchApiUriNotConfigured()
    {
        var sut = CreateSut(new TextServicesSettings { SearchApiUri = null });

        var result = await sut.GetTextAugmentedManifest("1/iiif/my-manifest");

        result.Should().BeNull();
        messageHandler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTextAugmentedManifest_ReturnsNull_When404()
    {
        var sut = CreateSut(new TextServicesSettings { SearchApiUri = new Uri("http://search-api/") });
        messageHandler.Enqueue(HttpStatusCode.NotFound);

        var result = await sut.GetTextAugmentedManifest("1/iiif/my-manifest");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTextAugmentedManifest_ReturnsDeserializedManifest_WhenSuccessful()
    {
        var sut = CreateSut(new TextServicesSettings { SearchApiUri = new Uri("http://search-api/") });
        var manifest = new Manifest { Id = "https://example.com/manifest" };
        messageHandler.Enqueue(HttpStatusCode.OK, manifest.AsJson());

        var result = await sut.GetTextAugmentedManifest("1/iiif/my-manifest");

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

        await sut.GetTextAugmentedManifest("1/iiif/my-manifest");

        var request = messageHandler.Requests.Single();
        request.Headers.GetValues("X-Forwarded-Host").Single().Should().Be("orchestrator.example.com");
        request.Headers.GetValues("X-Forwarded-Path").Single().Should().Be("/path/rules");
    }

    private class TestMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new();
        public List<HttpRequestMessage> Requests { get; } = [];

        public void Enqueue(HttpStatusCode statusCode, string? content = null)
        {
            var response = new HttpResponseMessage(statusCode);
            if (content != null)
                response.Content = new StringContent(content, Encoding.UTF8, "application/json");
            responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responses.Count > 0
                ? responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
