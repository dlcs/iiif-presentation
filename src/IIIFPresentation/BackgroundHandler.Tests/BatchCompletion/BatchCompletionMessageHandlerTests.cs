using AWS.Helpers;
using AWS.SQS;
using BackgroundHandler.BatchCompletion;
using BackgroundHandler.Tests.infrastructure;
using DLCS;
using DLCS.API;
using DLCS.Models;
using FakeItEasy;
using FluentAssertions;
using IIIF;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using Batch = Models.Database.General.Batch;
using CanvasPainting = Models.Database.CanvasPainting;
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
    private readonly int customerId = 1;

    public BatchCompletionMessageHandlerTests(PresentationContextFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        dlcsClient = A.Fake<IDlcsOrchestratorClient>();
        iiifS3 = A.Fake<IIIIFS3Service>();
        sut = new BatchCompletionMessageHandler(dbFixture.DbContext, dlcsClient, iiifS3,
            new NullLogger<BatchCompletionMessageHandler>());
    }

    [Fact]
    public async Task HandleMessage_False_IfMessageInvalid()
    {
        // Arrange
        var message = GetMessage("not-json");
        
        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeFalse();
    }
    
    [Fact]
    public async Task HandleMessage_DoesNotUpdateAnything_WhenBatchNotTracked()
    {
        // Arrange
        var message = GetMessage("{\"id\":572246,\"customerId\":58,\"total\":1,\"success\":1,\"errors\":0,\"superseded\":false,\"started\":\"2024-12-19T21:03:31.577678Z\",\"finished\":\"2024-12-19T21:03:31.902514Z\"}");
        
        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveImagesForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
    
    [Fact]
    public async Task HandleMessage_UpdatesBatchedImages_WhenBatchTracked()
    {
        // Arrange
        var batchId = 1;
        var manifestId = nameof(HandleMessage_UpdatesBatchedImages_WhenBatchTracked);
        var slug = $"slug_{nameof(HandleMessage_UpdatesBatchedImages_WhenBatchTracked)}";
        var assetId = $"asset_id_{nameof(HandleMessage_UpdatesBatchedImages_WhenBatchTracked)}";
        var space = 2;
        var fullAssetId = new AssetId(customerId, space, assetId);
        
        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIF.Presentation.V3.Manifest>(A<IHierarchyResource>._, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIF.Presentation.V3.Manifest
        {
            Id = manifestId
        });
        
        var manifest = CreateManifest(manifestId, slug, assetId, space, batchId);
        
        await dbContext.Manifests.AddAsync(manifest);
        await dbContext.SaveChangesAsync();

        var finished = DateTime.UtcNow.AddHours(-1);

        var batchMessage = $@"
{{
    ""id"":{batchId},
    ""customerId"": {customerId},
    ""total"":1,
    ""success"":1,
    ""errors"":0,
    ""superseded"":false,
    ""started"":""2024-12-19T21:03:31.57Z"",
    ""finished"":""{finished:yyyy-MM-ddTHH:mm:ssK}""
}}";
        
        var message = GetMessage(batchMessage);

        A.CallTo(() => dlcsClient.RetrieveImagesForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .Returns(new IIIF.Presentation.V3.Manifest
            {
                Items = new List<Canvas>
                {
                    new ()
                    {
                        Id = fullAssetId.ToString(),
                        Width = 100,
                        Height = 100,
                        Annotations = new List<AnnotationPage>
                        {
                            new ()
                            {
                                Items = new List<IAnnotation>()
                                {
                                    new PaintingAnnotation()
                                    {
                                        Body = new Image()
                                        {
                                            Width = 100,
                                            Height = 100,
                                        },
                                        Service = new List<IService>()
                                        {
                                            new ImageService3()
                                            {
                                                Profile = "level2"
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            });
        
        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveImagesForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .MustHaveHappened();
        var batch = dbContext.Batches.Single(b => b.Id == batchId);
        batch.Status.Should().Be(BatchStatus.Completed);
        batch.Processed.Should().BeCloseTo(finished, TimeSpan.FromSeconds(10));
        var canvasPainting = dbContext.CanvasPaintings.Single(c => c.AssetId == fullAssetId.ToString());
        canvasPainting.Ingesting.Should().BeFalse();
        canvasPainting.StaticWidth.Should().Be(100);
        canvasPainting.StaticHeight.Should().Be(100);
        canvasPainting.AssetId.Should()
            .Be(fullAssetId.ToString());
    }
    
    [Fact]
    public async Task HandleMessage_DoesNotUpdateBatchedImages_WhenAnotherBatchWaiting()
    {
        // Arrange
        var batchId = 2;
        var manifestId = nameof(HandleMessage_DoesNotUpdateBatchedImages_WhenAnotherBatchWaiting);
        var slug = $"slug_{nameof(HandleMessage_DoesNotUpdateBatchedImages_WhenAnotherBatchWaiting)}";
        var assetId = $"asset_id_{nameof(HandleMessage_DoesNotUpdateBatchedImages_WhenAnotherBatchWaiting)}";
        var space = 2;
        
        var manifest = CreateManifest(manifestId, slug, assetId, space, batchId);
        var additionalBatch = new Batch
        {
            Id = 3,
            ManifestId = manifestId,
            CustomerId = customerId,
            Status = BatchStatus.Ingesting
        };
        
        await dbContext.Manifests.AddAsync(manifest);
        await dbContext.Batches.AddAsync(additionalBatch);
        await dbContext.SaveChangesAsync();

        var batchMessage = $@"
{{
    ""id"":{batchId},
    ""customerId"": {customerId},
    ""total"": 1,
    ""success"": 1,
    ""errors"": 0,
    ""superseded"": false,
    ""started"": ""2024-12-19T21:03:31.57Z"",
    ""finished"": ""2024-12-19T21:03:31.57Z""
}}";
        
        var message = GetMessage(batchMessage);
        
        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveImagesForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    private Manifest CreateManifest(string manifestId, string slug, string assetId, int space, int batchId)
    {
        var manifest = new Manifest
        {
            Id = manifestId,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            CustomerId = customerId,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = slug,
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ],
            CanvasPaintings = [
                new CanvasPainting
                {
                    AssetId = $"{customerId}/{space}/{assetId}",
                    CanvasOrder = 0,
                    ChoiceOrder = 1,
                    CustomerId = customerId,
                    Ingesting = true
                }
            ],
            Batches = [
                new Batch
                {
                    Id = batchId,
                    CustomerId = customerId,
                    Submitted = DateTime.UtcNow,
                    Status = BatchStatus.Ingesting
                }
            ]
        };
        return manifest;
    }

    private static QueueMessage GetMessage(string body) => new(body, new Dictionary<string, string>(), "foo");
}
