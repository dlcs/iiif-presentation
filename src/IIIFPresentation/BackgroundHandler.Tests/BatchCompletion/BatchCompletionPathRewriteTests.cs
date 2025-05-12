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
using Models.DLCS;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using A = FakeItEasy.A;
using IIIFManifest = IIIF.Presentation.V3.Manifest;
using ResourceBase = IIIF.Presentation.V3.ResourceBase;

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
    public BatchCompletionPathRewriteTests(PresentationContextFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        dlcsClient = A.Fake<IDlcsOrchestratorClient>();
        iiifS3 = A.Fake<IIIIFS3Service>();
        
        backgroundHandlerSettings = new BackgroundHandlerSettings
        {
            PresentationApiUrl = "https://localhost:5000",
            CustomerPresentationApiUrl = new Dictionary<int, string>()
            {
                {1, "https://foo.com"},
                {2, "https://bar.com"}
            },
            PathRules = new TypedPathTemplateOptions
            {
                Overrides = new Dictionary<string, Dictionary<string, string>>
                {
                    // override everything
                    ["https://foo.com"] = new()
                    {
                        ["ManifestPrivate"] = "/foo/{customerId}/manifests/{resourceId}",
                        ["CollectionPrivate"] = "/foo/{customerId}/collections/{resourceId}",
                        ["ResourcePublic"] = "/foo/{customerId}/{hierarchyPath}",
                        ["Canvas"] = "/foo/{customerId}/canvases/{resourceId}"
                    },
                    // custom base URL
                    ["https://bar.com"] = new()
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
    public async Task HandleMessage_UpdatesBatchedImages_WithAllPathsRewritten()
    {
        // Arrange
        var batchId = 300;
        var customerId = 1;
        var identifier = nameof(HandleMessage_UpdatesBatchedImages_WithAllPathsRewritten);
        const int space = 2;

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, true, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest
        {
            Id = identifier
        });
        
        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId, id: identifier, customer: customerId);
        
        var assetId = new AssetId(customerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.SaveChangesAsync();

        var finished = DateTime.UtcNow.AddHours(-1);
        var message = QueueHelper.CreateQueueMessage(batchId, 1, finished);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, backgroundHandlerSettings.PresentationApiUrl));
        
        IIIFManifest updatedManifest = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<IHierarchyResource>._, A<string>._, A<bool>._, A<CancellationToken>._))
            .Invokes(x => updatedManifest =x.Arguments.Get<IIIFManifest>(0));
        
        // Act
        await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        updatedManifest.Id.Should().Be(identifier);
        updatedManifest.Items[0].Id.Should().Be("https://foo.com/foo/1/canvases/Models.Database.Collections.Manifest_1");
        updatedManifest.Items[0].Items[0].Id.Should().Be("https://foo.com/foo/1/canvases/Models.Database.Collections.Manifest_1/annopages/0");
        Fake.ClearRecordedCalls(iiifS3);
    }
    
    [Fact]
    public async Task HandleMessage_UpdatesBatchedImages_WithCustomBaseUrl()
    {
        // Arrange
        var customerId = 2;
        await dbContext.Collections.AddTestRootCollection(customerId);
        
        var batchId = 301;
        var identifier = nameof(HandleMessage_UpdatesBatchedImages_WithCustomBaseUrl);
        const int space = 2;

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, true, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest
            {
                Id = identifier
            });
        
        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId, id: identifier, customer: customerId);
        
        var assetId = new AssetId(customerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.SaveChangesAsync();

        var finished = DateTime.UtcNow.AddHours(-1);
        var message = QueueHelper.CreateQueueMessage(batchId, 1, finished);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, backgroundHandlerSettings.PresentationApiUrl));
        
        IIIFManifest updatedManifest = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<IHierarchyResource>._, A<string>._, A<bool>._, A<CancellationToken>._))
            .Invokes(x => updatedManifest =x.Arguments.Get<IIIFManifest>(0));
        
        // Act
        await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        updatedManifest.Id.Should().Be(identifier);
        updatedManifest.Items[0].Id.Should().Be("https://base/2/canvases/Models.Database.Collections.Manifest_1");
        updatedManifest.Items[0].Items[0].Id.Should().Be("https://base/2/canvases/Models.Database.Collections.Manifest_1/annopages/0");
        Fake.ClearRecordedCalls(iiifS3);
    }
    
    [Fact]
    public async Task HandleMessage_UpdatesBatchedImages_WithoutCustomerPresentationApiOverride()
    {
        // Arrange
        var customerId = 3;
        await dbContext.Collections.AddTestRootCollection(customerId);
        
        var batchId = 302;
        var identifier = nameof(HandleMessage_UpdatesBatchedImages_WithoutCustomerPresentationApiOverride);
        const int space = 2;

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, true, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest
            {
                Id = identifier
            });
        
        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId, id: identifier, customer: customerId);
        
        var assetId = new AssetId(customerId, space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.SaveChangesAsync();

        var finished = DateTime.UtcNow.AddHours(-1);
        var message = QueueHelper.CreateQueueMessage(batchId, 1, finished);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, backgroundHandlerSettings.PresentationApiUrl));
        
        IIIFManifest updatedManifest = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<IHierarchyResource>._, A<string>._, A<bool>._, A<CancellationToken>._))
            .Invokes(x => updatedManifest =x.Arguments.Get<IIIFManifest>(0));
        
        // Act
        await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        updatedManifest.Id.Should().Be(identifier);
        updatedManifest.Items[0].Id.Should().Be("https://localhost:5000/3/canvases/Models.Database.Collections.Manifest_1");
        updatedManifest.Items[0].Items[0].Id.Should().Be("https://localhost:5000/3/canvases/Models.Database.Collections.Manifest_1/annopages/0");
        Fake.ClearRecordedCalls(iiifS3);
    }
}
