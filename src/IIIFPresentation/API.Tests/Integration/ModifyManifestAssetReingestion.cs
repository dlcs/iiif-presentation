using System.Net;
using Amazon.S3;
using API.Infrastructure.Helpers;
using API.Tests.Integration.Infrastructure;
using DLCS.API;
using DLCS.Models;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.Database;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyManifestAssetReingestion: IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private const int Customer = 1;
    private const int NewlyCreatedSpace = 999;
    private static readonly IDlcsApiClient DLCSApiClient = A.Fake<IDlcsApiClient>();
    private static readonly IDlcsOrchestratorClient DLCSOrchestratorClient = A.Fake<IDlcsOrchestratorClient>();

    public ModifyManifestAssetReingestion(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        
        // Always return Space 999 when call to create space
        A.CallTo(() => DLCSApiClient.CreateSpace(Customer, A<string>._, A<CancellationToken>._))
            .Returns(new Space { Id = NewlyCreatedSpace, Name = "test" });
        
        // Echo back "batch" value set in first Asset
        A.CallTo(() => DLCSApiClient.IngestAssets(Customer, A<List<JObject>>._, A<CancellationToken>._))
            .ReturnsLazily(x => Task.FromResult(
                new List<Batch> { new ()
                {
                    ResourceId =  x.Arguments.Get<List<JObject>>("images").First().GetValue("batch").ToString(), 
                    Submitted = DateTime.Now
                }}));

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services => 
                services
                    .AddSingleton(DLCSApiClient)
                    .AddSingleton(DLCSOrchestratorClient));

        storageFixture.DbFixture.CleanUp();
    }
    
    [Fact]
    public async Task UpdateManifest_ReingestsAsset_WhenReingestingUntrackedAsset()
    {
        // Arrange
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: TestIdentifiers.BatchId(),
            ingested: true);
        await dbContext.SaveChangesAsync();
        
        var batchId = TestIdentifiers.BatchId();
        var payload = $$"""
                         {
                             "type": "Manifest",
                             "slug": "{{slug}}",
                             "parent": "http://localhost/{{Customer}}/collections/root",
                             "paintedResources": [
                                 {
                                     "asset": {
                                         "id": "{{assetId}}",
                                         "batch": "{{batchId}}",
                                         "mediaType": "image/jpg"
                                     },
                                     "reingest": true
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload, dbContext.GetETag(testManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("\"reingest\"");

        var canvasPaintings = await dbContext.CanvasPaintings.Where(cp => cp.ManifestId == id).ToListAsync();
        canvasPaintings.Should().HaveCount(1);
        canvasPaintings.First().Ingesting.Should().BeTrue();
        
        A.CallTo(() => DLCSApiClient.IngestAssets(A<int>._,
            A<List<JObject>>.That.Matches(o => o.First().GetValue("id").ToString() == assetId),
            A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task UpdateManifest_ReingestsAsset_WhenReingestingTrackedAsset()
    {
        // Arrange
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, assetId)
            }
        };

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();
        
        var batchId = TestIdentifiers.BatchId();
        var payload = $$"""
                         {
                             "type": "Manifest",
                             "slug": "{{slug}}",
                             "parent": "http://localhost/{{Customer}}/collections/root",
                             "paintedResources": [
                                 {
                                     "asset": {
                                         "id": "{{assetId}}",
                                         "batch": "{{batchId}}",
                                         "mediaType": "image/jpg"
                                     },
                                     "reingest": true
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload, dbContext.GetETag(testManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var canvasPaintings = await dbContext.CanvasPaintings.Where(cp => cp.ManifestId == id).ToListAsync();
        canvasPaintings.Should().HaveCount(1);
        canvasPaintings.First().Ingesting.Should().BeTrue();
        
        // ingest occurs for the asset, even though it's tracked
        A.CallTo(() => DLCSApiClient.IngestAssets(A<int>._,
            A<List<JObject>>.That.Matches(o => o.First().GetValue("id").ToString() == assetId),
            A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task UpdateManifest_ReingestsAsset_WhenReingestingTrackedAssetInAnotherManifest()
    {
        // Arrange
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, assetId),
                ManifestId = $"{id}_first"
            }
        };

        await dbContext.Manifests.AddTestManifest(id: $"{id}_first", slug: $"{slug}_first", canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: TestIdentifiers.BatchId(),
            ingested: true, spaceId: NewlyCreatedSpace);
        await dbContext.SaveChangesAsync();
        
        var batchId = TestIdentifiers.BatchId();
        var payload = $$"""
                         {
                             "type": "Manifest",
                             "slug": "{{slug}}",
                             "parent": "http://localhost/{{Customer}}/collections/root",
                             "paintedResources": [
                                 {
                                     "asset": {
                                         "id": "{{assetId}}",
                                         "batch": "{{batchId}}",
                                         "mediaType": "image/jpg"
                                     },
                                     "reingest": true
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload, dbContext.GetETag(testManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var canvasPaintings = await dbContext.CanvasPaintings.Where(cp => cp.ManifestId == id).ToListAsync();
        canvasPaintings.Should().HaveCount(1);
        canvasPaintings.First().Ingesting.Should().BeTrue();
        
        // ingest occurs for the asset, even though it's tracked
        A.CallTo(() => DLCSApiClient.IngestAssets(A<int>._,
            A<List<JObject>>.That.Matches(o => o.First().GetValue("id").ToString() == assetId),
            A<CancellationToken>._)).MustHaveHappened();
        
        A.CallTo(() => DLCSApiClient.UpdateAssetManifest(Customer,
            A<List<string>>.That.Matches(a => a.First() == $"{Customer}/{NewlyCreatedSpace}/{assetId}"),
            A<OperationType>.That.Matches(a => a == OperationType.Add),
            A<List<string>>._, A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task UpdateManifest_CreatesManifestAndReingestsAsset_WhenReingestingTrackedAssetInAnotherManifest()
    {
        // Arrange
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, assetId)
            }
        };

        await dbContext.Manifests.AddTestManifest(id: $"{id}_first", slug: $"{slug}_first", canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();
        
        var batchId = TestIdentifiers.BatchId();
        var payload = $$"""
                         {
                             "type": "Manifest",
                             "slug": "{{slug}}",
                             "parent": "http://localhost/{{Customer}}/collections/root",
                             "paintedResources": [
                                 {
                                     "asset": {
                                         "id": "{{assetId}}",
                                         "batch": "{{batchId}}",
                                         "mediaType": "image/jpg",
                                         "manifests": ["ignored"],
                                         "space": {{NewlyCreatedSpace}}
                                     },
                                     "reingest": true
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var canvasPaintings = await dbContext.CanvasPaintings.Where(cp => cp.ManifestId == id).ToListAsync();
        canvasPaintings.Should().HaveCount(1);
        canvasPaintings.First().Ingesting.Should().BeTrue();
        
        // ingest occurs for the asset, even though it's tracked
        A.CallTo(() => DLCSApiClient.IngestAssets(A<int>._,
            A<List<JObject>>.That.Matches(o => o.First().GetValue("id").ToString() == assetId),
            A<CancellationToken>._)).MustHaveHappened();
        
        // asset manifest value gets updated after the batch call
        A.CallTo(() => DLCSApiClient.UpdateAssetManifest(Customer,
            A<List<string>>.That.Matches(a => a.First() == $"{Customer}/{NewlyCreatedSpace}/{assetId}"),
            A<OperationType>.That.Matches(a => a == OperationType.Add),
            A<List<string>>._, A<CancellationToken>._)).MustHaveHappened();
    }
}
