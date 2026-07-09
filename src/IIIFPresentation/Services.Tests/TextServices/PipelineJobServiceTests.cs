using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.FakeItEasy;
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
        job.InvocationId.Should().BeNull("unset here - it's read from text-services' response once submitted");
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

    [Fact]
    public async Task DeletePipelineJob_CallsTextBuilderClient_WhenManifestHasPipelineJob()
    {
        var dbManifest = MakeManifest();
        var jobs = new List<PipelineJob>
        {
            new() { ManifestId = dbManifest.Id, CustomerId = dbManifest.CustomerId, JobType = PipelineJobType.TextService }
        };
        A.CallTo(() => dbContext.PipelineJobs).Returns(jobs.BuildMockDbSet());
        A.CallTo(() => textBuilderClient.DeleteJob(A<TextJobId>._, A<CancellationToken>._)).Returns(true);

        await sut.DeletePipelineJob(dbManifest, CancellationToken.None);

        A.CallTo(() => textBuilderClient.DeleteJob(
            A<TextJobId>.That.Matches(j => j.CustomerId == dbManifest.CustomerId && j.ResourceId == dbManifest.Id),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeletePipelineJob_DoesNotCallTextBuilderClient_WhenManifestHasNoPipelineJob()
    {
        var dbManifest = MakeManifest();
        A.CallTo(() => dbContext.PipelineJobs).Returns(new List<PipelineJob>().BuildMockDbSet());

        await sut.DeletePipelineJob(dbManifest, CancellationToken.None);

        A.CallTo(() => textBuilderClient.DeleteJob(A<TextJobId>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(PipelineJobStatus.NotSubmitted)]
    [InlineData(PipelineJobStatus.FailedToSubmit)]
    public async Task DeletePipelineJob_DoesNotCallTextBuilderClient_WhenJobNeverReachedTextServices(
        PipelineJobStatus status)
    {
        var dbManifest = MakeManifest();
        var jobs = new List<PipelineJob>
        {
            new() { ManifestId = dbManifest.Id, CustomerId = dbManifest.CustomerId, JobType = PipelineJobType.TextService, Status = status }
        };
        A.CallTo(() => dbContext.PipelineJobs).Returns(jobs.BuildMockDbSet());

        await sut.DeletePipelineJob(dbManifest, CancellationToken.None);

        A.CallTo(() => textBuilderClient.DeleteJob(A<TextJobId>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DeletePipelineJob_DoesNotThrow_WhenTextBuilderClientFails()
    {
        var dbManifest = MakeManifest();
        var jobs = new List<PipelineJob>
        {
            new() { ManifestId = dbManifest.Id, CustomerId = dbManifest.CustomerId, JobType = PipelineJobType.TextService }
        };
        A.CallTo(() => dbContext.PipelineJobs).Returns(jobs.BuildMockDbSet());
        A.CallTo(() => textBuilderClient.DeleteJob(A<TextJobId>._, A<CancellationToken>._)).Returns(false);

        var act = async () => await sut.DeletePipelineJob(dbManifest, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}