using System.Net;
using AWS.Settings;
using Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Database.General;
using Services.TextServices;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.Tests.TextServices;

public class TextBuilderClientTests
{
    private readonly TestMessageHandler messageHandler = new();

    private TextBuilderClient CreateSut(TextServicesSettings settings) =>
        new(new HttpClient(messageHandler), Options.Create(settings),
            Options.Create(new AWSSettings { S3 = new S3Settings { StorageBucket = "my-bucket" } }),
            new NullLogger<TextBuilderClient>());

    private static PipelineJob MakeJob(int customerId = 1, string resourceId = "my-manifest") =>
        new() { CustomerId = customerId, ManifestId = resourceId, JobType = PipelineJobType.TextService };

    private static DbManifest MakeManifest(int customerId = 1, string id = "my-manifest") =>
        new() { CustomerId = customerId, Id = id };

    [Fact]
    public async Task UpsertJob_ReturnsTrue_AndSetsJobWaiting_WhenPostSucceeds()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.OK);
        var job = MakeJob();

        var result = await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        result.Should().BeTrue();
        job.Status.Should().Be(PipelineJobStatus.Waiting);
        messageHandler.Requests.Single().Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task UpsertJob_SetsInvocationIdFromResponse_WhenPostSucceeds()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.OK, """{"invocationCount":1}""");
        var job = MakeJob();

        await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        job.InvocationId.Should().Be("1");
    }

    [Fact]
    public async Task UpsertJob_SetsInvocationIdFromResponse_WhenPutSucceedsAfterConflict()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.Conflict);
        messageHandler.Enqueue(HttpStatusCode.OK, """{"invocationCount":2}""");
        var job = MakeJob();

        await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        job.InvocationId.Should().Be("2", "text-services incremented its own counter on reprocess");
    }

    [Fact]
    public async Task UpsertJob_LeavesInvocationIdUnchanged_WhenResponseBodyIsUnparseable()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.OK, "not-json");
        var job = MakeJob();
        job.InvocationId = "7";

        var result = await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        result.Should().BeTrue("an unparseable response body shouldn't fail an otherwise-successful submission");
        job.InvocationId.Should().Be("7");
    }

    [Fact]
    public async Task UpsertJob_FallsBackToPut_WhenPostReturns409()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.Conflict);
        messageHandler.Enqueue(HttpStatusCode.OK);
        var job = MakeJob();

        var result = await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        result.Should().BeTrue();
        job.Status.Should().Be(PipelineJobStatus.Waiting);
        messageHandler.Requests.Should().HaveCount(2);
        messageHandler.Requests[0].Method.Should().Be(HttpMethod.Post);
        messageHandler.Requests[1].Method.Should().Be(HttpMethod.Put);
    }

    [Fact]
    public async Task UpsertJob_ReturnsFalse_AndSetsJobFailedToSubmit_WhenBuilderApiUriNotConfigured()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = null });
        var job = MakeJob();

        var result = await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        result.Should().BeFalse();
        job.Status.Should().Be(PipelineJobStatus.FailedToSubmit);
        job.Error.Should().Be("TextServices BuilderApiUri is not configured");
        messageHandler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertJob_ReturnsFalse_AndSetsJobFailedToSubmit_WhenPostReturnsNonSuccess()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.InternalServerError);
        var job = MakeJob();

        var result = await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        result.Should().BeFalse();
        job.Status.Should().Be(PipelineJobStatus.FailedToSubmit);
    }

    [Fact]
    public async Task UpsertJob_StoresErrorsFieldFromResponse_AsJobError_WhenPostReturnsNonSuccess()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.BadRequest, """{"errors":"sourceUri is invalid"}""");
        var job = MakeJob();

        await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        job.Error.Should().Be("sourceUri is invalid");
    }

    [Fact]
    public async Task UpsertJob_StoresFallbackMessage_AsJobError_WhenNonSuccessResponseHasNoBody()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.InternalServerError);
        var job = MakeJob();

        await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        job.Error.Should().Be("Text-services returned 500");
    }

    [Fact]
    public async Task UpsertJob_StoresFallbackMessage_AsJobError_WhenResponseBodyIsUnparseable()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.BadRequest, "not-json");
        var job = MakeJob();

        await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        job.Error.Should().Be("Text-services returned 400");
    }

    [Fact]
    public async Task UpsertJob_StoresErrorsFieldFromResponse_AsJobError_WhenPutReturnsNonSuccessAfterConflict()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.Conflict);
        messageHandler.Enqueue(HttpStatusCode.BadRequest, """{"errors":"reprocess rejected"}""");
        var job = MakeJob();

        await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        job.Status.Should().Be(PipelineJobStatus.FailedToSubmit);
        job.Error.Should().Be("reprocess rejected");
    }

    [Fact]
    public async Task UpsertJob_ReturnsFalse_AndSetsJobFailedToSubmit_WhenPostTimesOut()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.EnqueueException(new TaskCanceledException());
        var job = MakeJob();

        var result = await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        result.Should().BeFalse();
        job.Status.Should().Be(PipelineJobStatus.FailedToSubmit);
        messageHandler.Requests.Single().Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task UpsertJob_ReturnsFalse_AndSetsJobFailedToSubmit_WhenPutTimesOut()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.Conflict);
        messageHandler.EnqueueException(new TaskCanceledException());
        var job = MakeJob();

        var result = await sut.UpsertJob(MakeManifest(), job, CancellationToken.None);

        result.Should().BeFalse();
        job.Status.Should().Be(PipelineJobStatus.FailedToSubmit);
        messageHandler.Requests.Should().HaveCount(2);
        messageHandler.Requests[1].Method.Should().Be(HttpMethod.Put);
    }

    [Fact]
    public async Task UpsertJob_SendsCorrectJobIdAndS3Uri_InPostBody()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.OK);

        await sut.UpsertJob(MakeManifest(customerId: 5, id: "test-manifest"),
            MakeJob(customerId: 5, resourceId: "test-manifest"), CancellationToken.None);

        var body = await messageHandler.Requests.Single().Content!.ReadAsStringAsync();
        body.Should().Contain("\"id\":\"5/iiif/test-manifest\"");
        body.Should().Contain("\"sourceUri\":\"s3://my-bucket/staging/5/manifests/test-manifest\"");
    }

    [Fact]
    public async Task UpsertJob_SendsSearchAutocompleteTextAugmented_AsServicesField()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.OK);

        await sut.UpsertJob(MakeManifest(), MakeJob(), CancellationToken.None);

        var body = await messageHandler.Requests.Single().Content!.ReadAsStringAsync();
        var expected = (int)(JobServices.All);
        body.Should().Contain($"\"services\":{expected}");
    }

    [Fact]
    public async Task DeleteJob_ReturnsTrue_WhenDeleteSucceeds()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.OK);

        var result = await sut.DeleteJob(new TextJobId(1, "my-manifest"), CancellationToken.None);

        result.Should().BeTrue();
        var request = messageHandler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Delete);
        request.RequestUri!.ToString().Should().Be("http://text-services/textbuilder/1/iiif/my-manifest");
    }

    [Fact]
    public async Task DeleteJob_ReturnsTrue_WhenJobAlreadyGone()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.NotFound);

        var result = await sut.DeleteJob(new TextJobId(1, "my-manifest"), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteJob_ReturnsFalse_WhenBuilderApiUriNotConfigured()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = null });

        var result = await sut.DeleteJob(new TextJobId(1, "my-manifest"), CancellationToken.None);

        result.Should().BeFalse();
        messageHandler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteJob_ReturnsFalse_WhenDeleteReturnsNonSuccess()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.Enqueue(HttpStatusCode.InternalServerError);

        var result = await sut.DeleteJob(new TextJobId(1, "my-manifest"), CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteJob_ReturnsFalse_WhenDeleteTimesOut()
    {
        var sut = CreateSut(new TextServicesSettings { BuilderApiUri = new Uri("http://text-services/") });
        messageHandler.EnqueueException(new TaskCanceledException());

        var result = await sut.DeleteJob(new TextJobId(1, "my-manifest"), CancellationToken.None);

        result.Should().BeFalse();
    }
}
