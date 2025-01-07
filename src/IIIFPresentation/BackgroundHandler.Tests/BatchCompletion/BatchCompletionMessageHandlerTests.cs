using AWS.SQS;
using BackgroundHandler.BatchCompletion;
using BackgroundHandler.Tests.infrastructure;
using DLCS;
using DLCS.API;
using DLCS.Models;
using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Database;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using Batch = Models.Database.General.Batch;

namespace BackgroundHandler.Tests.BatchCompletion;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class BatchCompletionMessageHandlerTests
{
    private readonly PresentationContext dbContext;
    private readonly BatchCompletionMessageHandler sut;
    private readonly IDlcsApiClient dlcsClient;
    private readonly int customerId = 1;

    public BatchCompletionMessageHandlerTests(PresentationContextFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        dlcsClient = A.Fake<IDlcsApiClient>();
        var options = Options.Create(new DlcsSettings
        {
            ApiUri = new Uri("https://localhost"),
            OrchestratorUri = new Uri("https://dlcs.localhost"),
            ApiJwtKey = "d29ycnl1bmtub3duc29udm9sdW1lZ3Jvd3RoYnVzbG8="
            
        });
        sut = new BatchCompletionMessageHandler(dbFixture.DbContext, dlcsClient, options, new NullLogger<BatchCompletionMessageHandler>());
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
        A.CallTo(() => dlcsClient.RetrieveAllImages(A<int>._, A<List<string>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
    
    [Fact]
    public async Task HandleMessage_UpdatesBatchedImages_WhenBatchTracked()
    {
        // Arrange
        var batchId = 1;
        var collectionId = nameof(HandleMessage_UpdatesBatchedImages_WhenBatchTracked);
        var slug = $"slug_{nameof(HandleMessage_UpdatesBatchedImages_WhenBatchTracked)}";
        var assetId = $"asset_id_{nameof(HandleMessage_UpdatesBatchedImages_WhenBatchTracked)}";
        var space = 2;
        
        var manifest = CreateManifest(collectionId, slug, assetId, space, batchId);
        
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

        A.CallTo(() => dlcsClient.RetrieveAllImages(A<int>._, A<List<string>>._, A<CancellationToken>._))
            .Returns(new HydraCollection<Asset>([
                new Asset
                {
                    ResourceId = $"http://localhost/{space}/images/{assetId}",
                    Width = 100,
                    Height = 100,
                    Ingesting = false
                }
            ]));
        
        // Act
        var handleMessage = await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        handleMessage.Should().BeTrue();
        A.CallTo(() => dlcsClient.RetrieveAllImages(A<int>._, A<List<string>>._, A<CancellationToken>._))
            .MustHaveHappened();
        var batch = dbContext.Batches.Single(b => b.Id == batchId);
        batch.Status.Should().Be(BatchStatus.Completed);
        batch.Processed.Should().BeCloseTo(finished, TimeSpan.FromSeconds(10));
        var canvasPainting = dbContext.CanvasPaintings.Single(c => c.AssetId == $"{customerId}/{space}/{assetId}");
        canvasPainting.Ingesting.Should().BeFalse();
        canvasPainting.StaticWidth.Should().Be(100);
        canvasPainting.StaticHeight.Should().Be(100);
        canvasPainting.AssetId.Should()
            .Be($"{customerId}/{space}/{assetId}");
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
        A.CallTo(() => dlcsClient.RetrieveAllImages(A<int>._, A<List<string>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
    
    [Fact]
    public async Task HandleMessage_DoesNotUpdateBatchedImages_WhenImageStillIngesting()
    {
        // Arrange
        var batchId = 4;
        var manifestId = nameof(HandleMessage_DoesNotUpdateBatchedImages_WhenImageStillIngesting);
        var slug = $"slug_{nameof(HandleMessage_DoesNotUpdateBatchedImages_WhenImageStillIngesting)}";
        var assetId = $"asset_id_{nameof(HandleMessage_DoesNotUpdateBatchedImages_WhenImageStillIngesting)}";
        var space = 2;
        
        var manifest = CreateManifest(manifestId, slug, assetId, space, batchId);
        manifest.CanvasPaintings![0].Ingesting = false;
        
        await dbContext.Manifests.AddAsync(manifest);
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
        
        A.CallTo(() => dlcsClient.RetrieveAllImages(A<int>._, A<List<string>>._, A<CancellationToken>._))
            .Returns(new HydraCollection<Asset>([
                new Asset
                {
                    ResourceId = $"http://localhost/{space}/images/{assetId}",
                    Width = 100,
                    Height = 100,
                    Ingesting = true
                }
            ]));
        
        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        var canvasPainting = dbContext.CanvasPaintings.Single(cp => cp.AssetId == $"{customerId}/{space}/{assetId}");
        canvasPainting.StaticWidth.Should().BeNull();
        canvasPainting.StaticHeight.Should().BeNull();
    }
    
    [Fact]
    public async Task HandleMessage_DoesNotUpdateImage_WhenImageHasError()
    {
        // Arrange
        var batchId = 5;
        var manifestId = nameof(HandleMessage_DoesNotUpdateImage_WhenImageHasError);
        var slug = $"slug_{nameof(HandleMessage_DoesNotUpdateImage_WhenImageHasError)}";
        var assetId = $"asset_id_{nameof(HandleMessage_DoesNotUpdateImage_WhenImageHasError)}";
        var space = 2;
        
        var manifest = CreateManifest(manifestId, slug, assetId, space, batchId);
        manifest.CanvasPaintings![0].Ingesting = false;
        
        await dbContext.Manifests.AddAsync(manifest);
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
        
        A.CallTo(() => dlcsClient.RetrieveAllImages(A<int>._, A<List<string>>._, A<CancellationToken>._))
            .Returns(new HydraCollection<Asset>([
                new Asset
                {
                    ResourceId = $"http://localhost/{space}/images/{assetId}",
                    Width = 100,
                    Height = 100,
                    Ingesting = false,
                    Error = "ingestion error",
                }
            ]));
        
        // Act and Assert
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
        var canvasPainting = dbContext.CanvasPaintings.Single(cp => cp.AssetId == $"{customerId}/{space}/{assetId}");
        canvasPainting.StaticWidth.Should().BeNull();
        canvasPainting.StaticHeight.Should().BeNull();
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
