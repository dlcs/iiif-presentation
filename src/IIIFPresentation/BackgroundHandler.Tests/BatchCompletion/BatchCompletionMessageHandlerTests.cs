using System.Text;
using AWS.Helpers;
using AWS.SQS;
using BackgroundHandler.BatchCompletion;
using BackgroundHandler.Infrastructure;
using BackgroundHandler.Tests.Helpers;
using BackgroundHandler.Tests.infrastructure;
using Core.Settings;
using DLCS;
using DLCS.API;
using FakeItEasy;
using FluentAssertions;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Database.Collections;
using Models.Database.General;
using Models.DLCS;
using Repository;
using Repository.Paths;
using Services.Manifests;
using Services.Manifests.AWS;
using Services.Manifests.Helpers;
using Services.Manifests.Settings;
using Services.TextServices;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using IIIFManifest = IIIF.Presentation.V3.Manifest;
using Manifest = Models.Database.Collections.Manifest;

namespace BackgroundHandler.Tests.BatchCompletion;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class BatchCompletionMessageHandlerTests
{
    private readonly PresentationContext dbContext;
    private readonly BatchCompletionMessageHandler sut;
    private readonly IDlcsOrchestratorClient dlcsClient;
    private readonly IIIIFS3Service iiifS3;
    private readonly ITextBuilderClient textBuilderClient;
    private readonly BehaviourSettings behaviour = new();
    private readonly PathSettings pathSettings;
    private const int CustomerId = 1;
    private const int AlternativeCustomer = 10;

    public BatchCompletionMessageHandlerTests(PresentationContextFixture dbFixture)
    {
        // The context from dbFixture doesn't track changes so setup/assert
        dbContext = dbFixture.DbContext;
        dbFixture.CustomerIdProvider.SetCustomerId(CustomerId);

        // The context used by SUT should track to mimic context config in actual use
        var sutContext = dbFixture.GetNewPresentationContext(dbFixture.CustomerIdProvider);

        dlcsClient = A.Fake<IDlcsOrchestratorClient>();
        iiifS3 = A.Fake<IIIIFS3Service>();
        textBuilderClient = A.Fake<ITextBuilderClient>();
        A.CallTo(() => textBuilderClient.UpsertJob(A<Manifest>._, A<PipelineJob>._, A<CancellationToken>._))
            .Returns(true);

        pathSettings = new PathSettings
        {
            PresentationApiUrl = new Uri("https://localhost:5000")
        };

        var pathGenerator = new SettingsBasedPathGenerator(Options.Create(new DlcsSettings
        {
            ApiUri = new Uri("https://dlcs.api")
        }), new SettingsDrivenPresentationConfigGenerator(Options.Create(pathSettings)));

        var pathRewriteParser =
            new PathRewriteParser(Options.Create(PathRewriteOptions.Default), new NullLogger<PathRewriteParser>());

        var manifestMerger = new ManifestMerger(pathGenerator, pathRewriteParser, new NullLogger<ManifestMerger>());
        var dlcsManifestMerger = new DlcsManifestMerger(dlcsClient, manifestMerger, new NullLogger<DlcsManifestMerger>());
        var manifestS3Manager = new ManifestS3Manager(iiifS3, pathGenerator,
            new TestOptionsMonitor<BehaviourSettings>(behaviour), new NullLogger<ManifestS3Manager>());
        var customerIdProvider = new SetCustomerIdProvider();

        sut = new BatchCompletionMessageHandler(sutContext, customerIdProvider, manifestS3Manager, dlcsManifestMerger,
            textBuilderClient, new NullLogger<BatchCompletionMessageHandler>());
    }

    [Fact]
    public async Task HandleMessage_False_IfMessageInvalid()
    {
        // Arrange
        var message = new QueueMessage("not-json", new Dictionary<string, string>(), "foo");

        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task HandleMessage_ReturnsFalse_WhenBatchNotTracked_AndBelowRetryThreshold(int approximateReceiveCount)
    {
        // Arrange - batch unknown, message should be retried
        var message = QueueHelper.CreateQueueMessage(572246, CustomerId, approximateReceiveCount: approximateReceiveCount);

        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeFalse();
        A.CallTo(() =>
                dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public async Task HandleMessage_ReturnsTrue_WhenBatchNotTracked_AndAboveRetryThreshold(int approximateReceiveCount)
    {
        // Arrange - batch unknown but already retried enough, discard the message
        var message = QueueHelper.CreateQueueMessage(572246, CustomerId, approximateReceiveCount: approximateReceiveCount);

        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        A.CallTo(() =>
                dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(DeliverableType.Asset, DeliverableType.Asset)]
    [InlineData(DeliverableType.Adjunct, DeliverableType.Asset)]
    [InlineData(DeliverableType.Asset, DeliverableType.Adjunct)]
    [InlineData(DeliverableType.Adjunct, DeliverableType.Adjunct)]
    public async Task HandleMessage_DoesNotUpdateBatchedImages_WhenAnotherBatchWaiting_RegardlessOfType(
        DeliverableType dbType, DeliverableType messageType)
    {
        // Arrange
        var batchId = TestIdentifiers.BatchId();
        var asset = TestIdentifiers.Id();
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: $"{dbType}_{messageType}");
        var otherBatchId = TestIdentifiers.BatchId();
        const int space = 2;

        var manifest = await dbContext.Manifests.AddTestManifest(id: manifestId, batchId: batchId);
        manifest.Entity.Batches!.Single().DeliverableType = messageType;
        var assetId = new AssetId(CustomerId, space, asset);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.Batches.AddTestBatch(otherBatchId, manifest.Entity, dbType);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId, deliverableType: messageType);

        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        var batch = await dbContext.Batches.Include(b => b.Manifest).SingleAsync(b => b.Id == batchId);
        batch.Status.Should().Be(BatchStatus.Completed);
        batch.Manifest!.LastProcessed.Should().BeNull();
    }

    [Theory]
    [InlineData(DeliverableType.Asset)]
    [InlineData(DeliverableType.Adjunct)]
    public async Task HandleMessage_AlreadyCompletedBatch_DoesNotUpdateManifest(DeliverableType deliverableType)
    {
        // Arrange - the stored batch is already Completed, assert that in this instance we don't change the Processed
        // date and don't attempt to re-save the item in S3
        var batchId = TestIdentifiers.BatchId();
        var asset = TestIdentifiers.Id();
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: $"{deliverableType}");
        var otherBatchId = TestIdentifiers.BatchId();
        const int space = 2;
        
        // Set processed date far in past to assert it hasn't changed
        var processedDate = new DateTime(2020, 4, 30, 12, 17, 15, DateTimeKind.Utc);

        var manifest = await dbContext.Manifests.AddTestManifest(id: manifestId, batchId: batchId);
        var batchInDatabase = manifest.Entity.Batches!.Single();
        batchInDatabase.DeliverableType = deliverableType;
        batchInDatabase.Status = BatchStatus.Completed;
        batchInDatabase.Processed = processedDate;
        
        var assetId = new AssetId(CustomerId, space, asset);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.Batches.AddTestBatch(otherBatchId, manifest.Entity, deliverableType);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId, deliverableType: deliverableType);

        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        var batch = await dbContext.Batches.Include(b => b.Manifest).SingleAsync(b => b.Id == batchId);
        batch.Status.Should().Be(BatchStatus.Completed);
        batch.Processed.Should().Be(processedDate, "Process date hasn't changed");
    }
    
    [Theory]
    [InlineData(DeliverableType.Asset, true)]
    [InlineData(DeliverableType.Asset, false)]
    [InlineData(DeliverableType.Adjunct, false)]
    [InlineData(DeliverableType.Adjunct, true)]
    public async Task HandleMessage_SavesResultingManifest_ToS3(DeliverableType deliverableType, bool storeOriginal)
    {
        // Arrange
        var batchId = TestIdentifiers.BatchId();
        var (identifier, canvasPaintingId) = TestIdentifiers.IdCanvasPainting(nameof(HandleMessage_SavesResultingManifest_ToS3) + storeOriginal);
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: $"{deliverableType.ToString()}{storeOriginal.ToString()}");
        const int space = 2;
        var flatId = $"https://localhost:5000/1/manifests/{manifestId}";

        if (storeOriginal)
        {
            // Testing with stored original, setup mock for reading original-staging
            A.CallTo(() =>
                    iiifS3.ReadStreamFromS3(A<IHierarchyResource>._, BucketLocationType.OriginalStaging,
                        A<CancellationToken>._))
                .ReturnsLazily(() =>
                {
                    var data = Encoding.UTF8.GetBytes(manifestId);
                    return new MemoryStream(data);
                });
        }
        else
        {
            // Testing as pre-store-originals, set a future date to disable this behaviour
            behaviour.StoresPayloadsSince = DateTimeOffset.Now.AddMonths(1);
        }

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest
            {
                Id = identifier
            });

        var manifestEntityEntry = await dbContext.Manifests.AddTestManifest(id: manifestId, batchId: batchId);
        var manifest = manifestEntityEntry.Entity;
        manifest.Batches!.Single().DeliverableType = deliverableType;
        var assetId = new AssetId(CustomerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest, id: canvasPaintingId, assetId: assetId,
            canvasOrder: 1, ingesting: true);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId, deliverableType: deliverableType);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, pathSettings.PresentationApiUrl));
        ResourceBase? resourceBase = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<Manifest>.That.Matches(m => m.Id == manifestId),
                flatId, false, A<CancellationToken>._))
            .Invokes((ResourceBase arg1, IHierarchyResource _, string _, bool _, CancellationToken _) =>
                resourceBase = arg1);

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue("Message successfully handled");
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<Manifest>.That.Matches(m => m.Id == manifestId),
                flatId, false, A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
        
        if (storeOriginal)
        {
            A.CallTo(() => iiifS3.SaveToS3(A<IHierarchyResource>._, BucketLocationType.Original, A<string>._,  A<CancellationToken>._))
                .MustHaveHappened(1, Times.Exactly);
        }
        else
        {
            A.CallTo(() => iiifS3.SaveToS3(A<IHierarchyResource>._, BucketLocationType.Original, A<string>._,  A<CancellationToken>._))
                .MustNotHaveHappened();
        }
        
        var savedManifest = (IIIFManifest)resourceBase!;
        var expectedCanvasId = $"https://localhost:5000/1/canvases/{canvasPaintingId}";
        var firstCanvas = savedManifest.Items![0];
        firstCanvas.Id.Should().Be(expectedCanvasId, "Canvas Id overwritten");
        firstCanvas.Items![0].Id.Should().Be(
            $"https://localhost:5000/1/canvases/{canvasPaintingId}/annopages/1",
            "AnnotationPage Id overwritten");
        var paintingAnnotation = firstCanvas.GetFirstPaintingAnnotation()!;
        paintingAnnotation.Id.Should().Be($"https://localhost:5000/1/canvases/{canvasPaintingId}/annotations/1",
            "PaintingAnnotation Id overwritten");
        paintingAnnotation.Target.As<Canvas>().Id.Should().Be(expectedCanvasId, "Target Id matches canvasId");
    }

    [Theory]
    [InlineData(DeliverableType.Asset)]
    [InlineData(DeliverableType.Adjunct)]
    public async Task HandleMessage_SavesManifestLevelAdjuncts_WhenStubCanvasInNQ(DeliverableType deliverableType)
    {
        // Arrange
        var batchId = TestIdentifiers.BatchId();
        var (identifier, canvasPaintingId) = TestIdentifiers.IdCanvasPainting();
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: $"{deliverableType}_adjuncts");
        const int space = 2;
        var flatId = $"https://localhost:5000/1/manifests/{manifestId}";
        const string seeAlsoId = "https://example.com/mets.xml";
        const string renderingId = "https://example.com/document.pdf";
        const string annotationId = "https://example.com/annotations/1";
        var assetId = new AssetId(CustomerId, space, identifier);
        var stubAssetId = new AssetId(CustomerId, 0, $"Manifest_{manifestId}");

        // Testing as pre-store-originals, set a future date to disable this behaviour
        behaviour.StoresPayloadsSince = DateTimeOffset.Now.AddMonths(1);
        
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest { Id = identifier });

        var manifestEntityEntry = await dbContext.Manifests.AddTestManifest(id: manifestId, batchId: batchId);
        var manifest = manifestEntityEntry.Entity;
        manifest.Batches!.Single().DeliverableType = deliverableType;
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest, id: canvasPaintingId, assetId: assetId,
            canvasOrder: 1, ingesting: true);
        await dbContext.SaveChangesAsync();

        var nqManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithImage())
            .WithCanvas(stubAssetId, c => c.WithImage()
                .WithAdjunctSeeAlso(seeAlsoId)
                .WithAdjunctRendering(renderingId)
                .WithAdjunctAnnotation(annotationId))
            .Build();

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .Returns(nqManifest);

        ResourceBase? resourceBase = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<Manifest>.That.Matches(m => m.Id == manifestId),
                flatId, false, A<CancellationToken>._))
            .Invokes((ResourceBase arg1, IHierarchyResource _, string _, bool _, CancellationToken _) =>
                resourceBase = arg1);

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId, deliverableType: deliverableType);

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue();
        var savedManifest = (IIIFManifest)resourceBase!;
        savedManifest.Items.Should().HaveCount(1, "stub canvas must not appear in manifest items");
        savedManifest.SeeAlso.Should().ContainSingle(s => s.Id == seeAlsoId,
            "manifest-level seeAlso applied from stub canvas");
        savedManifest.Rendering.Should().ContainSingle(r => r.Id == renderingId,
            "manifest-level rendering applied from stub canvas");
        savedManifest.Annotations.Should().ContainSingle(a => a.Id == annotationId,
            "manifest-level annotations applied from stub canvas");
    }

    [Fact]
    public async Task HandleMessage_ReturnsFalse_NoException_WhenStagingMissing()
    {
        // Arrange
        var batchId = TestIdentifiers.BatchId();
        var (identifier, canvasPaintingId) = TestIdentifiers.IdCanvasPainting();
        const int space = 3;
        
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging, A<CancellationToken>._))
            .ReturnsLazily(() => (IIIFManifest?)null);

        var manifestEntityEntry = await dbContext.Manifests.AddTestManifest(identifier, batchId: batchId);
        var manifest = manifestEntityEntry.Entity;
        var assetId = new AssetId(CustomerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest, canvasPaintingId, assetId: assetId,
            canvasOrder: 1, ingesting: true);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, pathSettings.PresentationApiUrl));

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeFalse("ReadFromS3 returned null, false expected");
    }
    
    [Fact]
    public async Task HandleMessage_SavesResultingManifest_WhenAnotherCustomerIngestingSameManifestId()
    {
        // Arrange
        var initialBatchId = TestIdentifiers.BatchId();
        var (identifier, canvasPaintingId) = TestIdentifiers.IdCanvasPainting();
        const int space = 2;
        var assetId = new AssetId(CustomerId, space, identifier);
        
        // Testing as pre-store-originals, set a future date to disable this behaviour
        behaviour.StoresPayloadsSince = DateTimeOffset.Now.AddMonths(1);
        
        var otherCustomerManifest = await dbContext.Manifests.AddTestManifest(batchId: initialBatchId, customer: AlternativeCustomer, ingested: false);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(otherCustomerManifest.Entity, id: canvasPaintingId, assetId: assetId,
            canvasOrder: 1, ingesting: true);

        var batchId = TestIdentifiers.BatchId();
        var flatId = $"https://localhost:5000/1/manifests/{identifier}";

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest
            {
                Id = identifier
            });

        var manifestEntityEntry = await dbContext.Manifests.AddTestManifest(batchId: batchId);
        var manifest = manifestEntityEntry.Entity;
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest, id: canvasPaintingId, assetId: assetId,
            canvasOrder: 1, ingesting: true);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, pathSettings.PresentationApiUrl));
        ResourceBase? resourceBase = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<Manifest>.That.Matches(m => m.Id == manifest.Id),
                flatId, false, A<CancellationToken>._))
            .Invokes((ResourceBase arg1, IHierarchyResource _, string _, bool _, CancellationToken _) =>
                resourceBase = arg1);

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue("Message successfully handled");
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<Manifest>.That.Matches(m => m.Id == manifest.Id),
                flatId, false, A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
        var savedManifest = (IIIFManifest)resourceBase!;
        var expectedCanvasId = $"https://localhost:5000/1/canvases/{canvasPaintingId}";
        savedManifest.Items[0].Id.Should().Be(expectedCanvasId, "Canvas Id overwritten");
        savedManifest.Items[0].Items[0].Id.Should().Be(
            $"https://localhost:5000/1/canvases/{canvasPaintingId}/annopages/1",
            "AnnotationPage Id overwritten");
        var paintingAnnotation = savedManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>();
        paintingAnnotation.Id.Should().Be($"https://localhost:5000/1/canvases/{canvasPaintingId}/annotations/1",
            "PaintingAnnotation Id overwritten");
        paintingAnnotation.Target.As<Canvas>().Id.Should().Be(expectedCanvasId, "Target Id matches canvasId");
    }

    [Theory]
    [InlineData(DeliverableType.Asset)]
    [InlineData(DeliverableType.Adjunct)]
    public async Task HandleMessage_SavesMergedManifestToStaging_AndSubmitsTextJob_WhenPipelineJobPending(
        DeliverableType deliverableType)
    {
        // Arrange
        var batchId = TestIdentifiers.BatchId();
        var (identifier, canvasPaintingId) = TestIdentifiers.IdCanvasPainting(
            nameof(HandleMessage_SavesMergedManifestToStaging_AndSubmitsTextJob_WhenPipelineJobPending) + deliverableType);
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: $"{deliverableType}_pipeline");
        const int space = 2;
        var assetId = new AssetId(CustomerId, space, identifier);

        behaviour.StoresPayloadsSince = DateTimeOffset.Now.AddMonths(1);

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging,
                A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest { Id = identifier });

        var manifestEntityEntry = await dbContext.Manifests.AddTestManifest(id: manifestId, batchId: batchId);
        var manifest = manifestEntityEntry.Entity;
        manifest.Batches!.Single().DeliverableType = deliverableType;
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest, id: canvasPaintingId, assetId: assetId,
            canvasOrder: 1, ingesting: true);
        await dbContext.PipelineJobs.AddAsync(new PipelineJob
        {
            ManifestId = manifestId,
            CustomerId = CustomerId,
            JobType = PipelineJobType.TextService,
            Status = PipelineJobStatus.NotSubmitted,
            Created = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, pathSettings.PresentationApiUrl));

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId, deliverableType: deliverableType);

        // Act
        var result = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<Manifest>.That.Matches(m => m.Id == manifestId),
                A<string>._, true, A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<Manifest>.That.Matches(m => m.Id == manifestId),
                A<string>._, false, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => textBuilderClient.UpsertJob(A<Manifest>.That.Matches(m => m.Id == manifestId),
                A<PipelineJob>._, A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
        A.CallTo(() => iiifS3.DeleteIIIFFromS3(A<IHierarchyResource>._, A<bool>._))
            .MustNotHaveHappened();
        var pipelineJob = await dbContext.PipelineJobs.SingleAsync(p => p.ManifestId == manifestId);
        pipelineJob.Status.Should().Be(PipelineJobStatus.Waiting,
            "successful submission moves the job from NotSubmitted to Waiting for its completion notification");
    }

    [Fact]
    public async Task HandleMessage_SubmitsMostRecentNotSubmittedJob_WhenManifestHasMultiplePipelineJobs()
    {
        // Arrange - a manifest can accumulate multiple PipelineJob rows (each resubmission creates a new one for
        // history, see ManifestWriteServiceTests.Create_AddsNewPipelineJob_WhenJobAlreadyExistsForManifest), so more
        // than one can be NotSubmitted at once. The newest should be the one submitted, matching the "latest wins"
        // convention TextServiceJobCompletionMessageHandler already uses when resolving a completion notification.
        var batchId = TestIdentifiers.BatchId();
        var (identifier, canvasPaintingId) = TestIdentifiers.IdCanvasPainting(
            nameof(HandleMessage_SubmitsMostRecentNotSubmittedJob_WhenManifestHasMultiplePipelineJobs));
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "pipeline_multiple");
        const int space = 2;
        var assetId = new AssetId(CustomerId, space, identifier);

        behaviour.StoresPayloadsSince = DateTimeOffset.Now.AddMonths(1);

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging,
                A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest { Id = identifier });

        var manifestEntityEntry = await dbContext.Manifests.AddTestManifest(id: manifestId, batchId: batchId);
        var manifest = manifestEntityEntry.Entity;
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest, id: canvasPaintingId, assetId: assetId,
            canvasOrder: 1, ingesting: true);
        await dbContext.PipelineJobs.AddAsync(new PipelineJob
        {
            ManifestId = manifestId,
            CustomerId = CustomerId,
            JobType = PipelineJobType.TextService,
            Status = PipelineJobStatus.NotSubmitted,
            Created = DateTime.UtcNow.AddMinutes(-10)
        });
        var newestJobEntry = await dbContext.PipelineJobs.AddAsync(new PipelineJob
        {
            ManifestId = manifestId,
            CustomerId = CustomerId,
            JobType = PipelineJobType.TextService,
            Status = PipelineJobStatus.NotSubmitted,
            Created = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var newestJobId = newestJobEntry.Entity.Id;

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, pathSettings.PresentationApiUrl));

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId);

        // Act
        var result = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        A.CallTo(() => textBuilderClient.UpsertJob(A<Manifest>.That.Matches(m => m.Id == manifestId),
                A<PipelineJob>.That.Matches(j => j.Id == newestJobId), A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
    }

    [Fact]
    public async Task HandleMessage_ReturnsFalse_WhenPipelineJobPending_AndTextServicesFails()
    {
        // Arrange
        var batchId = TestIdentifiers.BatchId();
        var (identifier, canvasPaintingId) = TestIdentifiers.IdCanvasPainting();
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "pipeline_fail");
        const int space = 2;
        var assetId = new AssetId(CustomerId, space, identifier);

        behaviour.StoresPayloadsSince = DateTimeOffset.Now.AddMonths(1);

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, BucketLocationType.Staging,
                A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest { Id = identifier });

        A.CallTo(() => textBuilderClient.UpsertJob(A<Manifest>._, A<PipelineJob>._, A<CancellationToken>._))
            .Returns(false);

        var manifestEntityEntry = await dbContext.Manifests.AddTestManifest(id: manifestId, batchId: batchId);
        var manifest = manifestEntityEntry.Entity;
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest, id: canvasPaintingId, assetId: assetId,
            canvasOrder: 1, ingesting: true);
        await dbContext.PipelineJobs.AddAsync(new PipelineJob
        {
            ManifestId = manifestId,
            CustomerId = CustomerId,
            JobType = PipelineJobType.TextService,
            Status = PipelineJobStatus.NotSubmitted,
            Created = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, pathSettings.PresentationApiUrl));

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId);

        // Act
        var result = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        result.Should().BeFalse("text-services submission failed, message should be retried");
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<Manifest>.That.Matches(m => m.Id == manifestId),
                A<string>._, false, A<CancellationToken>._))
            .MustNotHaveHappened();
        var pipelineJob = await dbContext.PipelineJobs.SingleAsync(p => p.ManifestId == manifestId);
        pipelineJob.Status.Should().Be(PipelineJobStatus.NotSubmitted,
            "failed submission must not move the job out of NotSubmitted, so a retry picks it up again");
    }

    [Fact]
    public async Task HandleMessage_DoesNotUpdateBatch_WhenDeliverableTypeMismatch()
    {
        // Arrange - batch stored with Asset type, message arrives with Adjunct type
        var batchId = TestIdentifiers.BatchId();
        var asset = TestIdentifiers.Id();
        var manifestId = TestIdentifiers.IdWithSuffix(suffix: "type-mismatch");
        const int space = 2;

        var manifest = await dbContext.Manifests.AddTestManifest(id: manifestId, batchId: batchId);
        manifest.Entity.Batches!.Single().DeliverableType = DeliverableType.Asset; // stored as Asset
        var assetId = new AssetId(CustomerId, space, asset);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId, deliverableType: DeliverableType.Adjunct); // message is Adjunct

        // Act
        var result = await sut.HandleMessage(message, CancellationToken.None);

        // Assert - batch not found (type mismatch), so message is retried (false) on first receive
        result.Should().BeFalse("Batch not found due to type mismatch; message will be retried");
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        var batch = dbContext.Batches.Single(b => b.Id == batchId);
        batch.Status.Should().Be(BatchStatus.Ingesting, "Batch status not updated due to type mismatch");
        batch.Processed.Should().BeNull("Batch processed timestamp not set due to type mismatch");
    }
}
