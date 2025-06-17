using AWS.Helpers;
using AWS.Settings;
using AWS.SQS;
using BackgroundHandler.BatchCompletion;
using BackgroundHandler.Helpers;
using BackgroundHandler.Settings;
using BackgroundHandler.Tests.Helpers;
using BackgroundHandler.Tests.infrastructure;
using DLCS.API;
using FakeItEasy;
using FluentAssertions;
using IIIF;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Database.Collections;
using Models.Database.General;
using Models.DLCS;
using Repository;
using Repository.Paths;
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
    private readonly BackgroundHandlerSettings backgroundHandlerSettings;
    private const int CustomerId = 1;

    public BatchCompletionMessageHandlerTests(PresentationContextFixture dbFixture)
    {
        // The context from dbFixture doesn't track changes so setup/assert
        dbContext = dbFixture.DbContext;
        
        // The context used by SUT should track to mimic context config in actual use
        var sutContext = dbFixture.GetNewPresentationContext();
        
        dlcsClient = A.Fake<IDlcsOrchestratorClient>();
        iiifS3 = A.Fake<IIIIFS3Service>();

        backgroundHandlerSettings = new BackgroundHandlerSettings
        {
            PresentationApiUrl = new Uri("https://localhost:5000"),
            AWS = new AWSSettings(),
        };

        var presentationGenerator =
            new SettingsDrivenPresentationConfigGenerator(Options.Create(backgroundHandlerSettings));
        var pathGenerator = new TestPathGenerator(presentationGenerator);
        
        var manifestMerger = new ManifestMerger(pathGenerator, new NullLogger<ManifestMerger>());

        sut = new BatchCompletionMessageHandler(sutContext, dlcsClient, iiifS3, pathGenerator, manifestMerger,
            new NullLogger<BatchCompletionMessageHandler>());
    }

    [Fact]
    public async Task HandleMessage_False_IfMessageInvalid()
    {
        // Arrange
        var message = new QueueMessage("not-json", new Dictionary<string, string>(), "foo");

        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task HandleMessage_DoesNotUpdateAnything_WhenBatchNotTracked()
    {
        // Arrange
        var message = QueueHelper.CreateQueueMessage(572246, CustomerId);

        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        A.CallTo(() =>
                dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
    
    [Fact]
    public async Task HandleMessage_DoesNotUpdateBatchedImages_WhenAnotherBatchWaiting()
    {
        // Arrange
        var batchId = TestIdentifiers.BatchId();
        var identifier = TestIdentifiers.Id();
        var otherBatchId = TestIdentifiers.BatchId();
        const int space = 2;

        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId);
        var assetId = new AssetId(CustomerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.Batches.AddTestBatch(otherBatchId, manifest.Entity);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId);

        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        var batch = await dbContext.Batches.Include(b => b.Manifest).SingleAsync(b => b.Id == batchId);
        batch.Status.Should().Be(BatchStatus.Completed);
        batch.Manifest!.LastProcessed.Should().BeNull();
    }

    [Fact]
    public async Task HandleMessage_SavesResultingManifest_ToS3()
    {
        // Arrange
        var batchId = TestIdentifiers.BatchId();
        var (identifier, canvasPaintingId) = TestIdentifiers.IdCanvasPainting();
        const int space = 2;
        var flatId = $"https://localhost:5000/1/manifests/{identifier}";

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, true, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest
            {
                Id = identifier
            });

        var manifestEntityEntry = await dbContext.Manifests.AddTestManifest(batchId: batchId);
        var manifest = manifestEntityEntry.Entity;
        var assetId = new AssetId(CustomerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest, id: canvasPaintingId, assetId: assetId,
            canvasOrder: 1, ingesting: true);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, backgroundHandlerSettings.PresentationApiUrl));
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

    [Fact]
    public async Task HandleMessage_ReturnsFalse_NoException_WhenStagingMissing()
    {
        // Arrange
        var batchId = TestIdentifiers.BatchId();
        var (identifier, canvasPaintingId) = TestIdentifiers.IdCanvasPainting();
        const int space = 3;

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, true, A<CancellationToken>._))
            .ReturnsLazily(() => (IIIFManifest?)null);

        var manifestEntityEntry = await dbContext.Manifests.AddTestManifest(identifier, batchId: batchId);
        var manifest = manifestEntityEntry.Entity;
        var assetId = new AssetId(CustomerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest, canvasPaintingId, assetId: assetId,
            canvasOrder: 1, ingesting: true);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, backgroundHandlerSettings.PresentationApiUrl));

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeFalse("ReadFromS3 returned null, false expected");
    }
}

public class TestPathGenerator(IPresentationPathGenerator presentationPathGenerator) : PathGeneratorBase(presentationPathGenerator)
{
    protected override Uri DlcsApiUrl => new("https://dlcs.test");
}
