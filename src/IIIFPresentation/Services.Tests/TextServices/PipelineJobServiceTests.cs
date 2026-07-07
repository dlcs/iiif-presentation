using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Models.API.Manifest;
using Models.Database.General;
using Repository;
using Services.TextServices;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.Tests.TextServices;

public class PipelineJobServiceTests
{
    private readonly PresentationContext dbContext = A.Fake<PresentationContext>();
    private readonly ITextBuilderClient textBuilderClient = A.Fake<ITextBuilderClient>();
    private readonly PipelineJobService sut;

    public PipelineJobServiceTests()
    {
        sut = new PipelineJobService(dbContext, textBuilderClient, NullLogger<PipelineJobService>.Instance);
    }

    private static DbManifest MakeManifest(string id = "manifest1", int customerId = 1) =>
        new() { Id = id, CustomerId = customerId };

    [Fact]
    public async Task PersistPipelineJob_ReturnsJob_AndAddsToManifest_WhenPipelineHasRecognisedTextItem()
    {
        var dbManifest = MakeManifest();
        var pipeline = new List<PipelineItem>
        {
            new() { Name = "text", Config = new PipelineConfig { Action = "Index" } }
        };

        var job = await sut.PersistPipelineJob(dbManifest, pipeline, CancellationToken.None);

        job.Should().NotBeNull();
        job!.ManifestId.Should().Be(dbManifest.Id);
        job.CustomerId.Should().Be(dbManifest.CustomerId);
        job.JobType.Should().Be(PipelineJobType.TextService);
        job.Status.Should().Be(PipelineJobStatus.NotSubmitted);
        job.Config!.Action.Should().Be("Index");
        job.InvocationCount.Should().Be(1, "unset here - it's read from text-services' response once submitted");
        dbManifest.PipelineJobs.Should().ContainSingle().Which.Should().BeSameAs(job);
    }

    [Fact]
    public async Task PersistPipelineJob_ReturnsNull_AndDoesNotAddJob_WhenNoPipelineItemIsRecognised()
    {
        var dbManifest = MakeManifest();
        var pipeline = new List<PipelineItem> { new() { Name = "unknown", Config = new PipelineConfig { Action = "Index" } } };

        var job = await sut.PersistPipelineJob(dbManifest, pipeline, CancellationToken.None);

        job.Should().BeNull();
        dbManifest.PipelineJobs.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task PersistPipelineJob_SkipsUnrecognisedItems_AndUsesFirstRecognisedOne()
    {
        var dbManifest = MakeManifest();
        var pipeline = new List<PipelineItem>
        {
            new() { Name = "unknown", Config = new PipelineConfig { Action = "Index" } },
            new() { Name = "text", Config = new PipelineConfig { Action = "Index" } }
        };

        var job = await sut.PersistPipelineJob(dbManifest, pipeline, CancellationToken.None);

        job.Should().NotBeNull();
        job!.JobType.Should().Be(PipelineJobType.TextService);
        dbManifest.PipelineJobs.Should().ContainSingle();
    }

    [Fact]
    public async Task SubmitPipelineJob_ReturnsTrue_WhenTextBuilderClientSucceeds()
    {
        A.CallTo(() => textBuilderClient.UpsertJob(A<DbManifest>._, A<PipelineJob>._, A<CancellationToken>._))
            .Returns(true);
        var dbManifest = MakeManifest();
        var job = new PipelineJob { ManifestId = dbManifest.Id, CustomerId = dbManifest.CustomerId, JobType = PipelineJobType.TextService };

        var result = await sut.SubmitPipelineJob(dbManifest, job, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitPipelineJob_ReturnsFalse_WhenTextBuilderClientFails()
    {
        A.CallTo(() => textBuilderClient.UpsertJob(A<DbManifest>._, A<PipelineJob>._, A<CancellationToken>._))
            .Returns(false);
        var dbManifest = MakeManifest();
        var job = new PipelineJob { ManifestId = dbManifest.Id, CustomerId = dbManifest.CustomerId, JobType = PipelineJobType.TextService };

        var result = await sut.SubmitPipelineJob(dbManifest, job, CancellationToken.None);

        result.Should().BeFalse();
    }
}