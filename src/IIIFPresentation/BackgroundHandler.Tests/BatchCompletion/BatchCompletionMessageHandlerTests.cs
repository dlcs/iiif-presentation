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
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using A = FakeItEasy.A;
using IIIFManifest = IIIF.Presentation.V3.Manifest;
using Times = FakeItEasy.Times;

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
        dbContext = dbFixture.DbContext;
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
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

        sut = new BatchCompletionMessageHandler(dbFixture.DbContext, dlcsClient, iiifS3, pathGenerator,
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
    public async Task HandleMessage_UpdatesBatchedImages_WhenBatchTracked()
    {
        // Arrange
        const int batchId = 100;
        const string identifier = nameof(HandleMessage_UpdatesBatchedImages_WhenBatchTracked);
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
        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId, finished);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, backgroundHandlerSettings.PresentationApiUrl));

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
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

    [Fact]
    public async Task HandleMessage_UpdatesBatchedImages_WhenStaticSize()
    {
        // Arrange
        const int batchId = 101;
        const string identifier = nameof(HandleMessage_UpdatesBatchedImages_WhenStaticSize);
        const int space = 2;
        const string flatId = $"https://localhost:5000/1/manifests/{identifier}";

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, true, A<CancellationToken>._))
            .ReturnsLazily(() => new()
            {
                Id = identifier
            });

        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId);
        var assetId = new AssetId(CustomerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true,
            width: 60, height: 80);
        await dbContext.SaveChangesAsync();

        var finished = DateTime.UtcNow.AddHours(-1);
        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId, finished);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, backgroundHandlerSettings.PresentationApiUrl));
        ResourceBase? resourceBase = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, manifest.Entity, flatId, false, A<CancellationToken>._))
            .Invokes((ResourceBase arg1, IHierarchyResource _, string _, bool _, CancellationToken _) =>
                resourceBase = arg1);

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .MustHaveHappened();
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, manifest.Entity, flatId, false, A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
        var savedManifest = (IIIFManifest)resourceBase!;
        var image = (savedManifest.Items?[0].Items?[0].Items?[0] as PaintingAnnotation)?.Body as Image;
        image.Should().NotBeNull("an image was provided at this path");
        image!.Id.Should()
            .Be($"{backgroundHandlerSettings.PresentationApiUrl}iiif-img/{assetId}/full/60,80/0/default.jpg",
                "w,h set from statics set on canvasPainting");
        image.Width.Should().Be(60, "width taken from statics set on canvasPainting");
        image.Height.Should().Be(80, "height taken from statics set on canvasPainting");
    }
    
    [Fact]
    public async Task HandleMessage_UpdatesBatchedImages_WhenStaticSize_HandlingRewrittenPaths()
    {
        // Arrange
        const int batchId = 102;
        const string identifier = nameof(HandleMessage_UpdatesBatchedImages_WhenStaticSize_HandlingRewrittenPaths);
        const int space = 2;
        const string flatId = $"https://localhost:5000/1/manifests/{identifier}";

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, true, A<CancellationToken>._))
            .ReturnsLazily(() => new()
            {
                Id = identifier
            });

        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId);
        var assetId = new AssetId(CustomerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true,
            width: 60, height: 80);
        await dbContext.SaveChangesAsync();

        var finished = DateTime.UtcNow.AddHours(-1);
        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId, finished);

        var qualifiedAssetId = $"https://other.host/image/{assetId.Asset}/full/100,100/0/default.jpg";
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId,
                backgroundHandlerSettings.PresentationApiUrl, qualifiedAssetId));
        ResourceBase? resourceBase = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, manifest.Entity, flatId, false, A<CancellationToken>._))
            .Invokes((ResourceBase arg1, IHierarchyResource _, string _, bool _, CancellationToken _) =>
                resourceBase = arg1);

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .MustHaveHappened();
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, manifest.Entity, flatId, false, A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
        var savedManifest = (IIIFManifest)resourceBase!;
        var image = (savedManifest.Items?[0].Items?[0].Items?[0] as PaintingAnnotation)?.Body as Image;
        image.Should().NotBeNull("an image was provided at this path");
        image!.Id.Should().Be($"https://other.host/image/{assetId.Asset}/full/60,80/0/default.jpg",
            "w,h set from statics set on canvasPainting");
        image.Width.Should().Be(60, "width taken from statics set on canvasPainting");
        image.Height.Should().Be(80, "height taken from statics set on canvasPainting");
    }

    [Fact]
    public async Task HandleMessage_DoesNotUpdateBatchedImages_WhenAnotherBatchWaiting()
    {
        // Arrange
        const int batchId = 2;
        const string identifier = nameof(HandleMessage_DoesNotUpdateBatchedImages_WhenAnotherBatchWaiting);
        const int space = 2;

        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId);
        var assetId = new AssetId(CustomerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.Batches.AddTestBatch(batchId + 1, manifest.Entity);
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
        const int batchId = -1;
        const string identifier = nameof(HandleMessage_SavesResultingManifest_ToS3);
        const int space = 2;
        const string flatId = $"https://localhost:5000/1/manifests/{identifier}";
        const string canvasPaintingId = $"cp_{identifier}";

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
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, manifest, flatId, false, A<CancellationToken>._)).Invokes(
            (ResourceBase arg1, IHierarchyResource _, string _, bool _, CancellationToken _) =>
                resourceBase = arg1);

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue("Message successfully handled");
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, manifest, flatId, false, A<CancellationToken>._))
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
    public async Task HandleMessage_PreserveNonStandardContext()
    {
        // Arrange
        const int batchId = -321;
        const string identifier = nameof(HandleMessage_PreserveNonStandardContext);
        const int space = 2;
        const string flatId = $"https://localhost:5000/1/manifests/{identifier}";
        const string canvasPaintingId = $"cp_{identifier}";

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, true, A<CancellationToken>._))
            .ReturnsLazily(() => new()
            {
                Id = identifier
            });

        var manifestEntityEntry = await dbContext.Manifests.AddTestManifest(batchId: batchId);
        var manifest = manifestEntityEntry.Entity;
        var assetId = new AssetId(CustomerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest, canvasPaintingId, assetId: assetId,
            canvasOrder: 1, ingesting: true);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, CustomerId);

        var nqManifest = ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, backgroundHandlerSettings.PresentationApiUrl);
        const string nonStandardContext = "https://iiif.wellcomecollection.org/extensions/born-digital/context.json";
        nqManifest.EnsureContext(nonStandardContext);
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(nqManifest);
        ResourceBase? resourceBase = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, manifest, flatId, false, A<CancellationToken>._)).Invokes(
            (ResourceBase arg1, IHierarchyResource _, string _, bool _, CancellationToken _) =>
                resourceBase = arg1);

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue("Message successfully handled");
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, manifest, flatId, false, A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
        var savedManifest = (IIIFManifest)resourceBase!;
        switch (savedManifest.Context)
        {
            case List<string> lst:
                lst.Should().Contain(nonStandardContext);
                break;
            case string str:
                str.Should().Be(nonStandardContext);
                break;
            default:
                Assert.Fail("missing context!");
                break;
        }
    }

    [Fact]
    public async Task HandleMessage_ReturnsFalse_NoException_WhenStagingMissing()
    {
        // Arrange
        const int batchId = -123;
        const string identifier = nameof(HandleMessage_ReturnsFalse_NoException_WhenStagingMissing);
        const int space = 3;
        const string canvasPaintingId = $"cp_{identifier}";

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
