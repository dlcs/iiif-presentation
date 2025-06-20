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
using Manifests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Database.Collections;
using Models.DLCS;
using Repository;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
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
    private const int Space = 2;
    
    public BatchCompletionPathRewriteTests(PresentationContextFixture dbFixture)
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
            CustomerPresentationApiUrl = new Dictionary<int, Uri>
            {
                { 1, new Uri("https://foo.com") },
                { 2, new Uri("https://bar.com") }
            },
            PathRules = new TypedPathTemplateOptions
            {
                Overrides = new Dictionary<string, Dictionary<string, string>>
                {
                    // override everything
                    ["foo.com"] = new()
                    {
                        ["ManifestPrivate"] = "/foo/{customerId}/manifests/{resourceId}",
                        ["CollectionPrivate"] = "/foo/{customerId}/collections/{resourceId}",
                        ["ResourcePublic"] = "/foo/{customerId}/{hierarchyPath}",
                        ["Canvas"] = "/foo/{customerId}/canvases/{resourceId}"
                    },
                    // custom base URL
                    ["bar.com"] = new()
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
        
        var manifestMerger = new ManifestMerger(pathGenerator, new NullLogger<ManifestMerger>());

        sut = new BatchCompletionMessageHandler(sutContext, dlcsClient, iiifS3, pathGenerator, manifestMerger,
            new NullLogger<BatchCompletionMessageHandler>());
    }
    
    [Fact]
    public async Task HandleMessage_UpdatesBatchedImages_WithAllPathsRewritten()
    {
        // Arrange
        var customerId = 1;
        var batchId = TestIdentifiers.BatchId();
        var identifier = TestIdentifiers.Id();

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, true, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest
        {
            Id = identifier
        });
        
        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId, id: identifier, customer: customerId);
        
        var assetId = new AssetId(customerId, Space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, customerId);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, backgroundHandlerSettings.PresentationApiUrl));
        
        IIIFManifest updatedManifest = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<IHierarchyResource>._, A<string>.That.Contains(identifier), false,
                A<CancellationToken>._))
            .Invokes(x => updatedManifest = x.Arguments.Get<IIIFManifest>(0));
        
        // Act
        await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        updatedManifest.Id.Should().Be(identifier, "FlatId is set in id");
        updatedManifest.Items[0].Id.Should().Be("https://foo.com/foo/1/canvases/Models.Database.Collections.Manifest_1",
            "Customer1 has 'foo.com' configured as CustomerPresentationApiUrl and 'foo.com' overrides using relative paths");
        updatedManifest.Items[0].Items[0].Id.Should()
            .Be("https://foo.com/foo/1/canvases/Models.Database.Collections.Manifest_1/annopages/0",
                "Customer1 has 'foo.com' configured as CustomerPresentationApiUrl and 'foo.com' overrides using relative paths");
    }
    
    [Fact]
    public async Task HandleMessage_UpdatesBatchedImages_WithCustomBaseUrl()
    {
        // Arrange
        var customerId = 2;
        await dbContext.Collections.AddTestRootCollection(customerId);
        
        var batchId = TestIdentifiers.BatchId();
        var identifier = TestIdentifiers.Id();

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, true, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest
            {
                Id = identifier
            });
        
        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId, id: identifier, customer: customerId);
        
        var assetId = new AssetId(customerId, Space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, customerId);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, backgroundHandlerSettings.PresentationApiUrl));
        
        IIIFManifest updatedManifest = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<IHierarchyResource>._, A<string>.That.Contains(identifier), false,
                A<CancellationToken>._))
            .Invokes(x => updatedManifest = x.Arguments.Get<IIIFManifest>(0));
        
        // Act
        await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        updatedManifest.Id.Should().Be(identifier, "FlatId is set in id");
        updatedManifest.Items[0].Id.Should().Be("https://base/2/canvases/Models.Database.Collections.Manifest_1",
            "Customer2 has 'bar.com' configured as CustomerPresentationApiUrl and 'bar.com' overrides using absolute paths");
        updatedManifest.Items[0].Items[0].Id.Should().Be(
            "https://base/2/canvases/Models.Database.Collections.Manifest_1/annopages/0",
            "Customer2 has 'bar.com' configured as CustomerPresentationApiUrl and 'bar.com' overrides using absolute paths");
    }
    
    [Fact]
    public async Task HandleMessage_UpdatesBatchedImages_WithoutCustomerPresentationApiOverride()
    {
        // Arrange
        var customerId = 3;
        await dbContext.Collections.AddTestRootCollection(customerId);
        
        var batchId = TestIdentifiers.BatchId();
        var identifier = TestIdentifiers.Id();

        A.CallTo(() => iiifS3.ReadIIIFFromS3<IIIFManifest>(A<IHierarchyResource>._, true, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest
            {
                Id = identifier
            });
        
        var manifest = await dbContext.Manifests.AddTestManifest(batchId: batchId, id: identifier, customer: customerId);
        
        var assetId = new AssetId(customerId, Space, identifier);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(manifest.Entity, assetId: assetId, ingesting: true);
        await dbContext.SaveChangesAsync();

        var message = QueueHelper.CreateQueueMessage(batchId, customerId);

        A.CallTo(() => dlcsClient.RetrieveAssetsForManifest(A<int>._, A<List<int>>._, A<CancellationToken>._))
            .Returns(ManifestTestCreator.GenerateMinimalNamedQueryManifest(assetId, backgroundHandlerSettings.PresentationApiUrl));
        
        IIIFManifest updatedManifest = null;
        A.CallTo(() => iiifS3.SaveIIIFToS3(A<ResourceBase>._, A<IHierarchyResource>._, A<string>.That.Contains(identifier), false,
                A<CancellationToken>._))
            .Invokes(x => updatedManifest = x.Arguments.Get<IIIFManifest>(0));
        
        // Act
        await sut.HandleMessage(message, CancellationToken.None);

        // Assert
        updatedManifest.Id.Should().Be(identifier, "FlatId is set in id");
        updatedManifest.Items[0].Id.Should().Be("https://localhost:5000/3/canvases/Models.Database.Collections.Manifest_1");
        updatedManifest.Items[0].Items[0].Id.Should().Be("https://localhost:5000/3/canvases/Models.Database.Collections.Manifest_1/annopages/0");
    }
}
