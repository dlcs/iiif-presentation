using AWS.Helpers;
using AWS.Settings;
using AWS.SQS;
using BackgroundHandler.BatchCompletion;
using BackgroundHandler.Settings;
using BackgroundHandler.Tests.infrastructure;
using DLCS.API;
using FakeItEasy;
using FluentAssertions;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Models.Database.Collections;
using Models.Database.General;
using Models.DLCS;
using Repository;
using Repository.Paths;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using IIIFManifest = IIIF.Presentation.V3.Manifest;

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
        var pathGenerator = new TestPathGenerator();
        backgroundHandlerSettings = new BackgroundHandlerSettings()
        {
            PresentationApiUrl = "https://localhost:5000",
            AWS = new AWSSettings()
        };

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
        var message = CreateQueueMessage(572246);
        
        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
    
    [Fact]
    public async Task HandleMessage_UpdatesBatchedImages_WhenBatchTracked()
    {
        // Arrange
        const int batchId = 100;
        const string identifier = nameof(HandleMessage_UpdatesBatchedImages_WhenBatchTracked);
        const int space = 2;
        
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest
        {
            Id = identifier
        });
        
        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId);
        var assetId = new AssetId(CustomerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.SaveChangesAsync();

        var finished = DateTime.UtcNow.AddHours(-1);
        var message = CreateQueueMessage(batchId, finished);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(GenerateMinimalNamedQueryManifest(assetId));
        
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
        const string flatId = $"http://base/1/manifests/{identifier}";
        const string canvasPaintingId = $"cp_{identifier}";

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, A<CancellationToken>._))
            .ReturnsLazily(() => new()
            {
                Id = identifier
            });

        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId, spaceId: space);
        var assetId = new AssetId(CustomerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true,
            width: 80, height: 80);
        await dbContext.SaveChangesAsync();

        var finished = DateTime.UtcNow.AddHours(-1);
        var message = CreateQueueMessage(batchId, finished);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(GenerateMinimalNamedQueryManifest(assetId));
        ResourceBase? resourceBase = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, manifest.Entity, flatId, A<CancellationToken>._))
            .Invokes((ResourceBase arg1, IHierarchyResource _, string _, CancellationToken _) => resourceBase = arg1);

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .MustHaveHappened();
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, manifest.Entity, flatId, A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
        var savedManifest = (IIIFManifest) resourceBase!;
        var image = (savedManifest.Items?[0].Items?[0].Items?[0] as PaintingAnnotation)?.Body as Image;
        image.Should().NotBeNull("an image was provided at this path");
        image!.Width.Should().Be(80, "width taken from statics set on canvasPainting");
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
        
        var message = CreateQueueMessage(batchId);
        
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
        const string flatId = $"http://base/1/manifests/{identifier}";
        const string canvasPaintingId = $"cp_{identifier}";
        
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, A<CancellationToken>._))
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
        
        var message = CreateQueueMessage(batchId);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(GenerateMinimalNamedQueryManifest(assetId));
        ResourceBase? resourceBase = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, manifest, flatId, A<CancellationToken>._))
            .Invokes((ResourceBase arg1, IHierarchyResource _, string _, CancellationToken _) => resourceBase = arg1);

        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue("Message successfully handled");
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, manifest, flatId, A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
        var savedManifest = (IIIFManifest) resourceBase!;
        savedManifest.Items[0].Id.Should().Be($"http://base/1/canvases/{canvasPaintingId}", "Canvas Id overwritten");
        savedManifest.Items[0].Items[0].Id.Should().Be($"http://base/1/canvases/{canvasPaintingId}/annopages/1",
            "AnnotationPage Id overwritten");
        savedManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Id.Should().Be(
            $"http://base/1/canvases/{canvasPaintingId}/annotations/1", "PaintingAnnotation Id overwritten");
    }

    private IIIFManifest GenerateMinimalNamedQueryManifest(AssetId fullAssetId)
    {
        var qualifiedAssetId = $"{backgroundHandlerSettings.PresentationApiUrl}/iiif-img/{fullAssetId}";
        var canvasId = $"{qualifiedAssetId}/canvas/c/1";
        return new IIIFManifest
        {
            Items = new List<Canvas>
            {
                new ()
                {
                    Id = canvasId,
                    Width = 100,
                    Height = 100,
                    Items =
                    [
                        new()
                        {
                            Id = $"{canvasId}/page",
                            Items =
                            [
                                new PaintingAnnotation
                                {
                                    Id = $"{canvasId}/page/image",
                                    Body = new Image
                                    {
                                        Id = $"{qualifiedAssetId}/full/100,100/0/default.jpg",
                                        Width = 100,
                                        Height = 100,
                                        Service =
                                        [
                                            new ImageService3
                                            {
                                                Width = 75,
                                                Height = 75
                                            }
                                        ]
                                    },
                                    Service =
                                    [
                                        new ImageService3
                                        {
                                            Profile = "level2"
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
        };
    }

    private static QueueMessage CreateQueueMessage(int batchId, DateTime? finished = null, int? customerId = null)
    {
        var batchMessage = $@"
{{
    ""id"":{batchId},
    ""customerId"": {customerId ?? CustomerId},
    ""total"":1,
    ""success"":1,
    ""errors"":0,
    ""superseded"":false,
    ""started"":""2024-12-19T21:03:31.57Z"",
    ""finished"":""{finished ?? DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssK}""
}}";
        return new QueueMessage(batchMessage, new Dictionary<string, string>(), "foo");
    }
}

public class TestPathGenerator : PathGeneratorBase
{
    protected override string PresentationUrl => "http://base";
    protected override Uri DlcsApiUrl => new("https://dlcs.test");
}
