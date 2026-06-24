using Amazon.SQS.Model;
using AWS.Helpers;
using AWS.SQS;
using BackgroundHandler.TextCompletion;
using BackgroundHandler.Tests.infrastructure;
using FakeItEasy;
using FluentAssertions;
using IIIF.Presentation.V3;
using IIIF.Search.V1;
using IIIF.Search.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Services.Manifests.AWS;
using Services.TextServices;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using DbManifest = Models.Database.Collections.Manifest;
using IIIFManifest = IIIF.Presentation.V3.Manifest;

namespace BackgroundHandler.Tests.TextCompletion;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class TextServiceJobCompletionMessageHandlerTests
{
    private readonly PresentationContext dbContext;
    private readonly TextServiceJobCompletionMessageHandler sut;
    private readonly IManifestStorageManager manifestStorageManager;
    private readonly IIIIFS3Service iiifS3;
    private readonly ITextServicesClient textServicesClient;
    private const int CustomerId = 1;

    public TextServiceJobCompletionMessageHandlerTests(PresentationContextFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbFixture.CustomerIdProvider.SetCustomerId(CustomerId);

        var sutContext = dbFixture.GetNewPresentationContext(dbFixture.CustomerIdProvider);

        iiifS3 = A.Fake<IIIIFS3Service>();
        manifestStorageManager = A.Fake<IManifestStorageManager>();
        textServicesClient = A.Fake<ITextServicesClient>();

        sut = new TextServiceJobCompletionMessageHandler(
            sutContext,
            dbFixture.CustomerIdProvider,
            manifestStorageManager,
            iiifS3,
            textServicesClient,
            new NullLogger<TextServiceJobCompletionMessageHandler>());
    }

    [Theory]
    [InlineData("not-a-valid-id")]
    [InlineData("noSlashAtAll")]
    [InlineData("/iiif/resource")]
    public async Task HandleMessage_ReturnsTrue_WhenJobIdCannotBeParsed(string malformedJobId)
    {
        var message = CreateMessage(malformedJobId, PipelineJobStatus.Completed);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, A<BucketLocationType>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task HandleMessage_ReturnsFalse_WhenPipelineJobNotFound_BelowRetryThreshold(int receiveCount)
    {
        var message = CreateMessage("1/iiif/unknown-manifest", PipelineJobStatus.Completed, receiveCount);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeFalse();
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, A<BucketLocationType>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task HandleMessage_ReturnsTrue_WhenPipelineJobNotFound_AboveRetryThreshold(int receiveCount)
    {
        var message = CreateMessage("1/iiif/unknown-manifest-discard", PipelineJobStatus.Completed, receiveCount);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, A<BucketLocationType>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task HandleMessage_ReturnsFalse_WhenStagedManifestMissing()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_staging_missing");
        var jobId = $"{CustomerId}/iiif/{manifestId}";
        await SetupManifestWithPipelineJob(manifestId, jobId);

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging, A<CancellationToken>._))
            .Returns((IIIFManifest?)null);

        var message = CreateMessage(jobId, PipelineJobStatus.Completed);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeFalse();
        A.CallTo(() => manifestStorageManager.SaveManifestInStorage(A<IIIFManifest>._, A<DbManifest>._, A<string?>._, A<bool>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task HandleMessage_UpdatesStatusToFailed_WhenJobFailed()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_failed");
        var jobId = $"{CustomerId}/iiif/{manifestId}";
        await SetupManifestWithPipelineJob(manifestId, jobId);

        var message = CreateMessage(jobId, PipelineJobStatus.Failed, errors: "OCR timed out");

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();

        var job = dbContext.PipelineJobs.Single(p => p.ManifestId == manifestId);
        job.Status.Should().Be(PipelineJobStatus.Failed);
        job.Error.Should().Be("OCR timed out");

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, A<BucketLocationType>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => manifestStorageManager.SaveManifestInStorage(
                A<IIIFManifest>._, A<DbManifest>._, A<string?>._, A<bool>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => textServicesClient.GetTextAugmentedManifest(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => iiifS3.DeleteIIIFFromS3(A<IHierarchyResource>._, true))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleMessage_UpdatesStatusToCompleted_AndSavesManifest_WhenJobCompletedWithNoAugmentedServices()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_completed_no_services");
        var jobId = $"{CustomerId}/iiif/{manifestId}";
        await SetupManifestWithPipelineJob(manifestId, jobId);

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging, A<CancellationToken>._))
            .Returns(new IIIFManifest { Id = manifestId });
        A.CallTo(() => textServicesClient.GetTextAugmentedManifest(jobId, A<CancellationToken>._))
            .Returns((IIIFManifest?)null);

        var message = CreateMessage(jobId, PipelineJobStatus.Completed);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();

        var job = dbContext.PipelineJobs.Single(p => p.ManifestId == manifestId);
        job.Status.Should().Be(PipelineJobStatus.Completed);
        job.Error.Should().BeNull();

        A.CallTo(() => manifestStorageManager.SaveManifestInStorage(
                A<IIIFManifest>._, A<DbManifest>._, null, false, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => iiifS3.DeleteIIIFFromS3(A<IHierarchyResource>._, true))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleMessage_MergesSearchServicesIntoManifest_WhenAugmentedManifestHasServices()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_merged_services");
        var jobId = $"{CustomerId}/iiif/{manifestId}";
        await SetupManifestWithPipelineJob(manifestId, jobId);

        var stagedManifest = new IIIFManifest { Id = manifestId };
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging, A<CancellationToken>._))
            .Returns(stagedManifest);

        var searchService = new SearchService2 { Id = "https://search.example.com/search" };
        var augmentedManifest = new IIIFManifest
        {
            Service = [searchService]
        };
        A.CallTo(() => textServicesClient.GetTextAugmentedManifest(jobId, A<CancellationToken>._))
            .Returns(augmentedManifest);

        IIIFManifest? savedManifest = null;
        A.CallTo(() => manifestStorageManager.SaveManifestInStorage(
                A<IIIFManifest>._, A<DbManifest>._, null, false, A<CancellationToken>._))
            .Invokes((IIIFManifest m, DbManifest _, string? _, bool _, CancellationToken _) => savedManifest = m)
            .Returns(Task.CompletedTask);

        var message = CreateMessage(jobId, PipelineJobStatus.Completed);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();

        savedManifest.Should().NotBeNull();
        savedManifest!.Service.Should().ContainSingle(s => s.Id == searchService.Id);
    }

    [Fact]
    public async Task HandleMessage_DoesNotDuplicateServices_WhenAugmentedManifestContainsDuplicateServiceId()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_dedup_services");
        var jobId = $"{CustomerId}/iiif/{manifestId}";
        await SetupManifestWithPipelineJob(manifestId, jobId);

        const string serviceId = "https://search.example.com/search";
        var stagedManifest = new IIIFManifest
        {
            Id = manifestId,
            Service = [new SearchService2 { Id = serviceId }]
        };
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging, A<CancellationToken>._))
            .Returns(stagedManifest);

        var augmentedManifest = new IIIFManifest
        {
            Service = [new SearchService2 { Id = serviceId }]
        };
        A.CallTo(() => textServicesClient.GetTextAugmentedManifest(jobId, A<CancellationToken>._))
            .Returns(augmentedManifest);

        IIIFManifest? savedManifest = null;
        A.CallTo(() => manifestStorageManager.SaveManifestInStorage(
                A<IIIFManifest>._, A<DbManifest>._, null, false, A<CancellationToken>._))
            .Invokes((IIIFManifest m, DbManifest _, string? _, bool _, CancellationToken _) => savedManifest = m)
            .Returns(Task.CompletedTask);

        var message = CreateMessage(jobId, PipelineJobStatus.Completed);

        await sut.HandleMessage(message, CancellationToken.None);

        savedManifest!.Service.Should().HaveCount(1, "duplicate service ID should not be added twice");
    }

    [Fact]
    public async Task HandleMessage_MergesContextFromAugmentedManifest_WhenAugmentedManifestHasContext()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_context_merge");
        var jobId = $"{CustomerId}/iiif/{manifestId}";
        await SetupManifestWithPipelineJob(manifestId, jobId);

        const string searchContext = "http://iiif.io/api/search/2/context.json";
        var stagedManifest = new IIIFManifest { Id = manifestId };
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging, A<CancellationToken>._))
            .Returns(stagedManifest);

        var augmentedManifest = new IIIFManifest
        {
            Service = [new SearchService2 { Id = "https://search.example.com/search" }],
            Context = searchContext
        };
        A.CallTo(() => textServicesClient.GetTextAugmentedManifest(jobId, A<CancellationToken>._))
            .Returns(augmentedManifest);

        await sut.HandleMessage(CreateMessage(jobId, PipelineJobStatus.Completed), CancellationToken.None);

        stagedManifest.Context.Should().Be(searchContext);
    }

    [Fact]
    public async Task HandleMessage_DoesNotAddPresentation3Context_FromAugmentedManifest()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_context_p3_skip");
        var jobId = $"{CustomerId}/iiif/{manifestId}";
        await SetupManifestWithPipelineJob(manifestId, jobId);

        var stagedManifest = new IIIFManifest { Id = manifestId };
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging, A<CancellationToken>._))
            .Returns(stagedManifest);

        var augmentedManifest = new IIIFManifest
        {
            Service = [new SearchService2 { Id = "https://search.example.com/search" }],
            Context = IIIF.Presentation.Context.Presentation3Context
        };
        A.CallTo(() => textServicesClient.GetTextAugmentedManifest(jobId, A<CancellationToken>._))
            .Returns(augmentedManifest);

        await sut.HandleMessage(CreateMessage(jobId, PipelineJobStatus.Completed), CancellationToken.None);

        stagedManifest.Context.Should().BeNull();
    }

    [Fact]
    public async Task HandleMessage_SetsFinishedTimestamp_WhenJobCompletes()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_finished_completed");
        var jobId = $"{CustomerId}/iiif/{manifestId}";
        await SetupManifestWithPipelineJob(manifestId, jobId);

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging, A<CancellationToken>._))
            .Returns(new IIIFManifest { Id = manifestId });
        A.CallTo(() => textServicesClient.GetTextAugmentedManifest(jobId, A<CancellationToken>._))
            .Returns((IIIFManifest?)null);

        await sut.HandleMessage(CreateMessage(jobId, PipelineJobStatus.Completed), CancellationToken.None);

        var job = dbContext.PipelineJobs.Single(p => p.ManifestId == manifestId);
        job.Finished.Should().Be(new DateTime(2024, 6, 12, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task HandleMessage_SetsFinishedTimestamp_WhenJobFails()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_finished_failed");
        var jobId = $"{CustomerId}/iiif/{manifestId}";
        await SetupManifestWithPipelineJob(manifestId, jobId);

        await sut.HandleMessage(CreateMessage(jobId, PipelineJobStatus.Failed, errors: "OCR error"), CancellationToken.None);

        var job = dbContext.PipelineJobs.Single(p => p.ManifestId == manifestId);
        job.Finished.Should().Be(new DateTime(2024, 6, 12, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task HandleMessage_OnlyAddsSearchService2_WhenAugmentedManifestHasOtherServiceTypes()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_filter_services");
        var jobId = $"{CustomerId}/iiif/{manifestId}";
        await SetupManifestWithPipelineJob(manifestId, jobId);

        var stagedManifest = new IIIFManifest { Id = manifestId };
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging, A<CancellationToken>._))
            .Returns(stagedManifest);

        var searchService = new SearchService2 { Id = "https://search.example.com/search" };
        var otherService = new SearchService { Id = "https://image.example.com/image" };
        var augmentedManifest = new IIIFManifest
        {
            Service = [searchService, otherService]
        };
        A.CallTo(() => textServicesClient.GetTextAugmentedManifest(jobId, A<CancellationToken>._))
            .Returns(augmentedManifest);

        IIIFManifest? savedManifest = null;
        A.CallTo(() => manifestStorageManager.SaveManifestInStorage(
                A<IIIFManifest>._, A<DbManifest>._, null, false, A<CancellationToken>._))
            .Invokes((IIIFManifest m, DbManifest _, string? _, bool _, CancellationToken _) => savedManifest = m)
            .Returns(Task.CompletedTask);

        await sut.HandleMessage(CreateMessage(jobId, PipelineJobStatus.Completed), CancellationToken.None);

        savedManifest!.Service.Should().ContainSingle()
            .Which.Should().BeOfType<SearchService2>();
    }

    private async Task SetupManifestWithPipelineJob(string manifestId, string jobId)
    {
        var manifestEntry = await dbContext.Manifests.AddTestManifest(id: manifestId);
        var manifest = manifestEntry.Entity;
        await dbContext.PipelineJobs.AddAsync(new PipelineJob
        {
            ManifestId = manifest.Id,
            JobType = PipelineJobType.TextService,
            CustomerId = manifest.CustomerId,

            Status = PipelineJobStatus.Waiting,
            Created = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static QueueMessage CreateMessage(string jobId, PipelineJobStatus status, int approximateReceiveCount = 0, string? errors = null)
    {
        var errorsJson = errors == null ? "null" : $"\"{errors}\"";
        var body = $$"""{"jobId":"{{jobId}}","status":{{(int)status}},"finished":"2024-06-12T10:00:00Z","totalPages":1,"totalWordCount":100,"errors":{{errorsJson}}}""";
        var systemAttributes = new Dictionary<string, string>
        {
            ["ApproximateReceiveCount"] = approximateReceiveCount.ToString()
        };
        return new QueueMessage(body, new Dictionary<string, MessageAttributeValue>(), systemAttributes, $"msg-{jobId}");
    }
}
