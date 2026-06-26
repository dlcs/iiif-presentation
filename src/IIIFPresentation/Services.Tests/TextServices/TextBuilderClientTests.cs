using System.Net;
using Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Database.General;
using Services.TextServices;

namespace Services.Tests.TextServices;

public class TextBuilderClientTests
{
    private readonly TestMessageHandler messageHandler = new();

    private TextBuilderClient CreateSut(TextServicesSettings settings) =>
        new(new HttpClient(messageHandler), Options.Create(settings), new NullLogger<TextBuilderClient>());

    private static PipelineJob MakeJob(int customerId = 1, string resourceId = "my-manifest") =>
        new() { CustomerId = customerId, ManifestId = resourceId, JobType = PipelineJobType.TextService };

    [Fact]
    public async Task CreateOrUpdateJob_ReturnsTrue_WhenPostSucceeds()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.OK);

        var result = await sut.CreateOrUpdateJob(MakeJob(), "my-bucket", "staging/1/manifests/my-manifest",
            CancellationToken.None);

        result.Should().BeTrue();
        messageHandler.Requests.Single().Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task CreateOrUpdateJob_FallsBackToPut_WhenPostReturns409()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.Conflict);
        messageHandler.Enqueue(HttpStatusCode.OK);

        var result = await sut.CreateOrUpdateJob(MakeJob(), "my-bucket", "staging/1/manifests/my-manifest",
            CancellationToken.None);

        result.Should().BeTrue();
        messageHandler.Requests.Should().HaveCount(2);
        messageHandler.Requests[0].Method.Should().Be(HttpMethod.Post);
        messageHandler.Requests[1].Method.Should().Be(HttpMethod.Put);
    }

    [Fact]
    public async Task CreateOrUpdateJob_ReturnsFalse_WhenBuilderApiUriNotConfigured()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = null });

        var result = await sut.CreateOrUpdateJob(MakeJob(), "my-bucket", "staging/1/manifests/my-manifest",
            CancellationToken.None);

        result.Should().BeFalse();
        messageHandler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateOrUpdateJob_ReturnsFalse_WhenPostReturnsNonSuccess()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.InternalServerError);

        var result = await sut.CreateOrUpdateJob(MakeJob(), "my-bucket", "staging/1/manifests/my-manifest",
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CreateOrUpdateJob_SendsCorrectJobIdAndS3Uri_InPostBody()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.OK);

        await sut.CreateOrUpdateJob(MakeJob(customerId: 5, resourceId: "test-manifest"), "my-bucket",
            "staging/5/manifests/test-manifest", CancellationToken.None);

        var body = await messageHandler.Requests.Single().Content!.ReadAsStringAsync();
        body.Should().Contain("\"id\":\"5/iiif/test-manifest\"");
        body.Should().Contain("\"sourceUri\":\"s3://my-bucket/staging/5/manifests/test-manifest\"");
    }

    [Fact]
    public async Task CreateOrUpdateJob_SendsSearchAutocompleteTextAugmented_AsServicesField()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.OK);

        await sut.CreateOrUpdateJob(MakeJob(), "my-bucket", "staging/1/manifests/my-manifest",
            CancellationToken.None);

        var body = await messageHandler.Requests.Single().Content!.ReadAsStringAsync();
        var expected = (int)(JobServices.All);
        body.Should().Contain($"\"services\":{expected}");
    }
}
