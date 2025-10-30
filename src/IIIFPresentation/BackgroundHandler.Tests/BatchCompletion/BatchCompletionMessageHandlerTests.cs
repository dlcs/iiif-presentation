using AWS.Helpers;
using AWS.SQS;
using BackgroundHandler.BatchCompletion;
using BackgroundHandler.Tests.Helpers;
using BackgroundHandler.Tests.infrastructure;
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
using Services.Manifests;
using Services.Manifests.AWS;
using Services.Manifests.Helpers;
using Services.Manifests.Settings;
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
    private readonly PathSettings pathSettings;
    private const int CustomerId = 1;

    public BatchCompletionMessageHandlerTests(PresentationContextFixture dbFixture)
    {
        // The context from dbFixture doesn't track changes so setup/assert
        dbContext = dbFixture.DbContext;
        
        // The context used by SUT should track to mimic context config in actual use
        var sutContext = dbFixture.GetNewPresentationContext();
        
        dlcsClient = A.Fake<IDlcsOrchestratorClient>();
        iiifS3 = A.Fake<IIIIFS3Service>();

        pathSettings = new PathSettings()
        {
            PresentationApiUrl = new Uri("https://localhost:5000")
        };

        var pathGenerator = new SettingsBasedPathGenerator(Options.Create(new DlcsSettings
        {
            ApiUri = new Uri("https://dlcs.api")
        }), new SettingsDrivenPresentationConfigGenerator(Options.Create(pathSettings)));
        
        var manifestMerger = new ManifestMerger(pathGenerator, new NullLogger<ManifestMerger>());
        var manifestS3Manager = new ManifestS3Manager(iiifS3, pathGenerator, dlcsClient, manifestMerger,
            new NullLogger<ManifestS3Manager>());

        sut = new BatchCompletionMessageHandler(sutContext, manifestS3Manager,
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
                dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
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
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
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

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, pathSettings.PresentationApiUrl));

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeFalse("ReadFromS3 returned null, false expected");
    }
    
    [Fact]
    public async Task HandleMessage_UpdatesBatchedImages_WhenOldStyleBatchCompletion()
    {
        // Arrange
        const int batchId = 124;
        const string identifier = nameof(HandleMessage_UpdatesBatchedImages_WhenOldStyleBatchCompletion);
        const int space = 2;

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, true, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest
            {
                Id = identifier
            });

        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId);
        var assetId = new AssetId(CustomerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.SaveChangesAsync();

        var finished = DateTime.UtcNow.AddHours(-1);
        var message = QueueHelper.CreateOldQueueMessage(batchId, CustomerId, finished);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, pathSettings.PresentationApiUrl));

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .MustHaveHappened();

        var batch = dbContext.Batches.Include(b => b.Manifest).Single(b => b.Id == batchId);
        batch.Status.Should().Be(BatchStatus.Completed);
        batch.Finished.Should().BeCloseTo(finished, TimeSpan.FromSeconds(10));
        batch.Processed.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        batch.Manifest!.LastProcessed.Should().NotBeNull();

        var canvasPainting = dbContext.CanvasPaintings.Single(c => c.AssetId == assetId);
        canvasPainting.Ingesting.Should().BeFalse();
        canvasPainting.StaticWidth.Should().Be(75, "width taken from NQ manifest image->imageService");
        canvasPainting.StaticHeight.Should().Be(75, "height taken from NQ manifest image->imageService");
        canvasPainting.AssetId!.ToString().Should().Be(assetId.ToString());
    }
}
