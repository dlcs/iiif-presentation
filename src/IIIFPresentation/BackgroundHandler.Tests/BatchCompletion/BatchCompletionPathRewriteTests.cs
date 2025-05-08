using AWS.Helpers;
using AWS.Settings;
using BackgroundHandler.BatchCompletion;
using BackgroundHandler.Helpers;
using BackgroundHandler.Settings;
using BackgroundHandler.Tests.Helpers;
using BackgroundHandler.Tests.infrastructure;
using Core.Web;
using DLCS.API;
using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Database.Collections;
using Models.Database.General;
using Models.DLCS;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using A = FakeItEasy.A;
using IIIFManifest = IIIF.Presentation.V3.Manifest;

namespace BackgroundHandler.Tests.BatchCompletion;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class BatchCompletionPathRewriteTests
{
    private readonly PresentationContext dbContext;
    private readonly BatchCompletionMessageHandler sut;
    private readonly IDlcsOrchestratorClient dlcsClient;
    private readonly IIIIFS3Service iiifS3;
    private readonly BackgroundHandlerSettings backgroundHandlerSettings;
    private const int CustomerId = 1;

    public BatchCompletionPathRewriteTests(PresentationContextFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        dlcsClient = A.Fake<IDlcsOrchestratorClient>();
        iiifS3 = A.Fake<IIIIFS3Service>();
        
        backgroundHandlerSettings = new BackgroundHandlerSettings
        {
            PresentationApiUrl = "https://localhost:5000",
            CustomerPresentationApiUrl = new Dictionary<string, string>()
            {
                {"1", "foo"},
                {"2", "bar"},
                {"3", "baz"}
            },
            PathRules = new TypedPathTemplateOptions
            {
                Overrides = new Dictionary<string, Dictionary<string, string>>
                {
                    // override everything
                    ["foo"] = new()
                    {
                        ["ManifestPrivate"] = "/foo/{customerId}/manifests/{resourceId}",
                        ["CollectionPrivate"] = "/foo/{customerId}/collections/{resourceId}",
                        ["ResourcePublic"] = "/foo/{customerId}/{hierarchyPath}",
                        ["Canvas"] = "/foo/{customerId}/canvases/{resourceId}"
                    },
                    // fallback to defaults
                    ["bar"] = new()
                    {
                        ["ResourcePublic"] = "/bar/{customerId}/{hierarchyPath}"
                    },
                    // custom base URL
                    ["baz"] = new()
                    {
                        ["ManifestPrivate"] = "https://base/{customerId}/manifests/{resourceId}",
                        ["CollectionPrivate"] = "https://base/{customerId}/collections/{resourceId}",
                        ["ResourcePublic"] = "https://base/{customerId}/{hierarchyPath}",
                        ["Canvas"] = "https://base/{customerId}/canvases/{resourceId}",
                    }
                }
            },
            AWS = new AWSSettings(),
        };

        var presentationGenerator =
            new SettingsDrivenPresentationConfigGenerator(Options.Create(backgroundHandlerSettings));
        var pathGenerator = new TestPathGenerator(presentationGenerator);

        sut = new BatchCompletionMessageHandler(dbFixture.DbContext, dlcsClient, iiifS3, pathGenerator,
            new NullLogger<BatchCompletionMessageHandler>());
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
        var message = QueueHelper.CreateQueueMessage(batchId, 1, finished);

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
}
