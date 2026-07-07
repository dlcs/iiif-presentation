using Amazon.SQS.Model;
using AWS.SQS;
using BackgroundHandler.TextCompletion;
using BackgroundHandler.Tests.infrastructure;
using FakeItEasy;
using FluentAssertions;
using IIIF;
using IIIF.ImageApi.V3;
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
    private readonly ITextSearchClient textServicesClient;
    private const int CustomerId = 1;

    public TextServiceJobCompletionMessageHandlerTests(PresentationContextFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbFixture.CustomerIdProvider.SetCustomerId(CustomerId);

        var sutContext = dbFixture.GetNewPresentationContext(dbFixture.CustomerIdProvider);

        manifestStorageManager = A.Fake<IManifestStorageManager>();
        textServicesClient = A.Fake<ITextSearchClient>();

        // Use a real augmentor (with the faked text-services client) so the existing augmentation
        // assertions continue to exercise the search-service merge logic through the handler
        var textManifestAugmentor =
            new TextManifestAugmentor(textServicesClient, new NullLogger<TextManifestAugmentor>());

        sut = new TextServiceJobCompletionMessageHandler(
            sutContext,
            dbFixture.CustomerIdProvider,
            manifestStorageManager,
            textManifestAugmentor,
            new NullLogger<TextServiceJobCompletionMessageHandler>());
    }

    private void SetupStagedManifest(IIIFManifest? manifest, string? original = null) =>
        A.CallTo(() => manifestStorageManager.ReadStagedManifest(A<DbManifest>._, A<CancellationToken>._))
            .Returns(new StagedManifest(manifest, original));

    [Theory]
    [InlineData("not-a-valid-id")]
    [InlineData("noSlashAtAll")]
    [InlineData("/iiif/resource")]
    public async Task HandleMessage_ReturnsTrue_WhenJobIdCannotBeParsed(string malformedJobId)
    {
        var message = CreateMessageFromRawJobId(malformedJobId, PipelineJobStatus.Completed);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        A.CallTo(() => manifestStorageManager.ReadStagedManifest(A<DbManifest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task HandleMessage_ReturnsFalse_WhenPipelineJobNotFound_BelowRetryThreshold(int receiveCount)
    {
        var message = CreateMessage(new TextJobId(CustomerId, "unknown-manifest"), PipelineJobStatus.Completed, receiveCount);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeFalse();
        A.CallTo(() => manifestStorageManager.ReadStagedManifest(A<DbManifest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task HandleMessage_ReturnsTrue_WhenPipelineJobNotFound_AboveRetryThreshold(int receiveCount)
    {
        var message = CreateMessage(new TextJobId(CustomerId, "unknown-manifest-discard"), PipelineJobStatus.Completed, receiveCount);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        A.CallTo(() => manifestStorageManager.ReadStagedManifest(A<DbManifest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task HandleMessage_ReturnsFalse_WhenStagedManifestMissing()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_staging_missing");
        var jobId = new TextJobId(CustomerId, manifestId);
        await SetupManifestWithPipelineJob(manifestId);

        SetupStagedManifest(null);

        var message = CreateMessage(jobId, PipelineJobStatus.Completed);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeFalse();
        A.CallTo(() => manifestStorageManager.SaveManifestInStorage(A<IIIFManifest>._, A<DbManifest>._, A<string?>._, A<bool>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(PipelineJobStatus.Failed)]
    [InlineData(PipelineJobStatus.Running)]
    [InlineData(PipelineJobStatus.Waiting)]
    public async Task HandleMessage_UpdatesStatus_WhenJobNotCompleted(PipelineJobStatus status)
    {
        // NOTE - the text-service should only ever return Completed or Failed but handle the given status, this allows
        // text-services to add additional completion statuses that may be returned in the future
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: status.ToString());
        var jobId = new TextJobId(CustomerId, manifestId);
        await SetupManifestWithPipelineJob(manifestId);

        var message = CreateMessage(jobId, status, errors: "Text extraction timed out");

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();

        var job = dbContext.PipelineJobs.Single(p => p.ManifestId == manifestId);
        job.Status.Should().Be(status);
        job.Error.Should().Be("Text extraction timed out");

        A.CallTo(() => manifestStorageManager.ReadStagedManifest(A<DbManifest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => manifestStorageManager.SaveManifestInStorage(
                A<IIIFManifest>._, A<DbManifest>._, A<string?>._, A<bool>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => textServicesClient.GetTextAugmentedManifest(A<TextJobId>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => manifestStorageManager.DeleteStagedManifest(A<DbManifest>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleMessage_UpdatesStatusToCompleted_AndSavesManifest_WhenJobCompletedWithNoAugmentedServices()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_completed_no_services");
        var jobId = new TextJobId(CustomerId, manifestId);
        await SetupManifestWithPipelineJob(manifestId);

        SetupStagedManifest(new IIIFManifest { Id = manifestId });
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
        A.CallTo(() => manifestStorageManager.DeleteStagedManifest(A<DbManifest>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleMessage_PromotesStoredOriginalPayload_WhenJobCompleted()
    {
        // The original payload stored alongside the staged manifest must be promoted to the final location
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_promote_original");
        var jobId = new TextJobId(CustomerId, manifestId);
        await SetupManifestWithPipelineJob(manifestId);

        const string original = "{\"original\":\"payload\"}";
        SetupStagedManifest(new IIIFManifest { Id = manifestId }, original);
        A.CallTo(() => textServicesClient.GetTextAugmentedManifest(jobId, A<CancellationToken>._))
            .Returns((IIIFManifest?)null);

        (await sut.HandleMessage(CreateMessage(jobId, PipelineJobStatus.Completed), CancellationToken.None))
            .Should().BeTrue();

        A.CallTo(() => manifestStorageManager.SaveManifestInStorage(
                A<IIIFManifest>._, A<DbManifest>._, original, false, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleMessage_MergesSearchServicesIntoManifest_WhenAugmentedManifestHasServices()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_merged_services");
        var jobId = new TextJobId(CustomerId, manifestId);
        await SetupManifestWithPipelineJob(manifestId);

        var stagedManifest = new IIIFManifest
        {
            Id = manifestId,
            Service = [new ImageService3 { Id = "https://image.example.com" }]
        };
        SetupStagedManifest(stagedManifest);

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
        savedManifest!.Service.Should().HaveCount(2, "Original image service not overwritten");
        savedManifest!.Service.Should().ContainSingle(s => s.Id == searchService.Id);
    }
    
    [Fact]
    public async Task HandleMessage_MergesSearchServicesIntoManifest_WhenAugmentedManifestHasServices_IfDBPipelineJobAlreadyComplete()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_merged_services");
        var jobId = new TextJobId(CustomerId, manifestId);
        await SetupManifestWithPipelineJob(manifestId, PipelineJobStatus.Completed, DateTime.UtcNow.AddDays(-1));

        var stagedManifest = new IIIFManifest
        {
            Id = manifestId,
            Service = [new ImageService3 { Id = "https://image.example.com" }]
        };
        SetupStagedManifest(stagedManifest);

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
        savedManifest!.Service.Should().HaveCount(2, "Original image service not overwritten");
        savedManifest!.Service.Should().ContainSingle(s => s.Id == searchService.Id);
    }

    [Fact]
    public async Task HandleMessage_DoesNotDuplicateServices_WhenAugmentedManifestContainsDuplicateServiceId()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_dedup_services");
        var jobId = new TextJobId(CustomerId, manifestId);
        await SetupManifestWithPipelineJob(manifestId);

        const string serviceId = "https://search.example.com/search";
        var stagedManifest = new IIIFManifest
        {
            Id = manifestId,
            Service = [new SearchService2 { Id = serviceId, Profile = "original" }]
        };
        SetupStagedManifest(stagedManifest);

        var augmentedManifest = new IIIFManifest
        {
            Service = [new SearchService2 { Id = serviceId, Profile = "incoming" }]
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

        savedManifest!.Service.Should().HaveCount(1, "Duplicate service ID should not be added twice");
        savedManifest.Service!.Single().As<SearchService2>().Profile.Should()
            .Be("original", "Original is not ovewritten");
        stagedManifest.Context.Should().BeNull("Context not added as no service added");
    }

    [Fact]
    public async Task HandleMessage_AddsSearch2Context_IfSearchServiceAdded()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_context_p3_skip");
        var jobId = new TextJobId(CustomerId, manifestId);
        await SetupManifestWithPipelineJob(manifestId);

        var stagedManifest = new IIIFManifest { Id = manifestId };
        SetupStagedManifest(stagedManifest);

        var augmentedManifest = new IIIFManifest
        {
            Service = [new SearchService2 { Id = "https://search.example.com/search" }],
            Context = IIIF.Presentation.Context.Presentation3Context
        };
        A.CallTo(() => textServicesClient.GetTextAugmentedManifest(jobId, A<CancellationToken>._))
            .Returns(augmentedManifest);

        await sut.HandleMessage(CreateMessage(jobId, PipelineJobStatus.Completed), CancellationToken.None);

        stagedManifest.Context.Should().Be("http://iiif.io/api/search/2/context.json");
    }

    [Fact]
    public async Task HandleMessage_SetsFinishedTimestamp_WhenJobCompletes()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_finished_completed");
        var jobId = new TextJobId(CustomerId, manifestId);
        await SetupManifestWithPipelineJob(manifestId);

        SetupStagedManifest(new IIIFManifest { Id = manifestId });
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
        var jobId = new TextJobId(CustomerId, manifestId);
        await SetupManifestWithPipelineJob(manifestId);

        await sut.HandleMessage(CreateMessage(jobId, PipelineJobStatus.Failed, errors: "OCR error"), CancellationToken.None);

        var job = dbContext.PipelineJobs.Single(p => p.ManifestId == manifestId);
        job.Finished.Should().Be(new DateTime(2024, 6, 12, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task HandleMessage_OnlyAddsSearchService2_WhenAugmentedManifestHasOtherServiceTypes()
    {
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_filter_services");
        var jobId = new TextJobId(CustomerId, manifestId);
        await SetupManifestWithPipelineJob(manifestId);

        var stagedManifest = new IIIFManifest { Id = manifestId };
        SetupStagedManifest(stagedManifest);

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

        savedManifest!.Service.Should().ContainSingle().Which.Should().Be(searchService);
    }
    
    [Fact]
    public async Task HandleMessage_SetsLabelOnSearchService2_AndAutoCompleteService()
    {
        var manifestId = TestIdentifiers.IdWithSuffix();
        var jobId = new TextJobId(CustomerId, manifestId);
        await SetupManifestWithPipelineJob(manifestId);

        var stagedManifest = new IIIFManifest { Id = manifestId };
        SetupStagedManifest(stagedManifest);

        var searchService = new SearchService2
        {
            Id = "https://search.example.com/search",
            Service = [new AutoCompleteService2 { Id = "https://search.example.com/autocomplete" }]
        };
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

        var savedSearchSvc = savedManifest!.Service!.Single().As<SearchService2>();
        savedSearchSvc.Label!.Values.Should().ContainSingle("Search within this manifest");
        var savedAutoCompleteSvc = savedSearchSvc!.Service!.Single().As<AutoCompleteService2>();
        savedAutoCompleteSvc.Label!.Values.Should().ContainSingle("Autocomplete words in this manifest");
    }

    [Fact]
    public async Task HandleMessage_MatchesJobByInvocationCount_NotByNewest_WhenMultipleJobsExist()
    {
        // A resubmission ("reprocess") creates a new PipelineJob row without removing the old one, so two rows
        // can legitimately co-exist for the same manifest. The completion notification's InvocationCount must be
        // used to pick the matching row - "newest wins" would incorrectly complete the wrong one here, since the
        // older invocation (1) is the one whose completion has just arrived, after the newer one (2) was already
        // recorded as still Waiting.
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "_invocation_count_match");
        var jobId = new TextJobId(CustomerId, manifestId);
        await dbContext.Manifests.AddTestManifest(id: manifestId)
            .WithTestPipelineJob(PipelineJobStatus.Waiting, created: DateTime.UtcNow.AddMinutes(-1), invocationCount: 1)
            .WithTestPipelineJob(PipelineJobStatus.Waiting, created: DateTime.UtcNow, invocationCount: 2);
        await dbContext.SaveChangesAsync();

        SetupStagedManifest(new IIIFManifest { Id = manifestId });
        A.CallTo(() => textServicesClient.GetTextAugmentedManifest(jobId, A<CancellationToken>._))
            .Returns((IIIFManifest?)null);

        var message = CreateMessage(jobId, PipelineJobStatus.Completed, invocationCount: 1);

        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();

        var job = dbContext.PipelineJobs.Single(p => p.ManifestId == manifestId && p.InvocationCount == 1);
        job.Status.Should().Be(PipelineJobStatus.Completed);

        var otherJob = dbContext.PipelineJobs.Single(p => p.ManifestId == manifestId && p.InvocationCount == 2);
        otherJob.Status.Should().Be(PipelineJobStatus.Waiting, "the newer invocation is unrelated to this notification");
    }

    private async Task SetupManifestWithPipelineJob(string manifestId,
        PipelineJobStatus status = PipelineJobStatus.Waiting, DateTime? finished = null)
    {
        await dbContext.Manifests.AddTestManifest(id: manifestId)
            .WithTestPipelineJob(status, finished);
        await dbContext.SaveChangesAsync();
    }

    private static QueueMessage CreateMessage(TextJobId jobId, PipelineJobStatus status, int approximateReceiveCount = 0,
        string? errors = null, int invocationCount = 1)
        => CreateMessageFromRawJobId(jobId.ToString(), status, approximateReceiveCount, errors, invocationCount);

    private static QueueMessage CreateMessageFromRawJobId(string jobId, PipelineJobStatus status, int approximateReceiveCount = 0,
        string? errors = null, int invocationCount = 1)
    {
        var errorsJson = errors == null ? "null" : $"\"{errors}\"";
        var body = $$"""{"jobId":"{{jobId}}","status":{{(int)status}},"finished":"2024-06-12T10:00:00Z","totalPages":1,"totalWordCount":100,"errors":{{errorsJson}},"invocationCount":{{invocationCount}}}""";
        var systemAttributes = new Dictionary<string, string>
        {
            ["ApproximateReceiveCount"] = approximateReceiveCount.ToString()
        };
        return new QueueMessage(body, new Dictionary<string, MessageAttributeValue>(), systemAttributes, $"msg-{jobId}");
    }
}
