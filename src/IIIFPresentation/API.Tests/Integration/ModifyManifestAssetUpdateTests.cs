using System.Net;
using Amazon.S3;
using API.Infrastructure.Helpers;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using DLCS.API;
using DLCS.Exceptions;
using DLCS.Models;
using FakeItEasy;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.General;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using Batch = DLCS.Models.Batch;
using CanvasPainting = Models.Database.CanvasPainting;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyManifestAssetUpdateTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private const int Customer = 1;
    private const int NewlyCreatedSpace = 999;
    private readonly IAmazonS3 amazonS3;
    private static readonly IDlcsApiClient DLCSApiClient = A.Fake<IDlcsApiClient>();
    private readonly IETagManager etagManager;
    
    public ModifyManifestAssetUpdateTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        
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
        
        dbContext = storageFixture.DbFixture.DbContext;

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services => services.AddSingleton(DLCSApiClient));
        
        etagManager = (IETagManager)factory.Services.GetRequiredService(typeof(IETagManager));

        storageFixture.DbFixture.CleanUp();
    }
    
    [Fact]
    public async Task UpdateManifest_AddSingleAsset_WhenNoneExist()
    {
        // Arrange
        var (slug, id, assetId) = TestIdentifiers.SlugResourceAsset();
        
        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                A<IList<string>>.That.Matches(l => l.Contains($"{Customer}/{NewlyCreatedSpace}/{assetId}")),
                A<CancellationToken>._)).ReturnsLazily(() => new List<JObject>())
            .Once().Then
            .ReturnsLazily(() =>
            [
                JObject.Parse($"{{\"@id\": \"https://dlcs.test/customers/1/spaces/999/images/{assetId}\"}}")
            ]);

        var batchId = TestIdentifiers.BatchId();
        var payload = $$"""
                         {
                             "type": "Manifest",
                             "slug": "{{slug}}",
                             "parent": "http://localhost/{{Customer}}/collections/root",
                             "paintedResources": [
                                 {
                                    "canvasPainting":{
                                        "label": {
                                             "en": [
                                                 "canvas testing"
                                             ]
                                         }
                                    },
                                     "asset": {
                                         "id": "{{assetId}}",
                                         "batch": "{{batchId}}",
                                         "mediaType": "image/jpg"
                                     }
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        
        responseManifest.Ingesting.Should().BeEquivalentTo(new IngestingAssets
        {
            Total = 1,
            Errors = 0,
            Finished = 0
        });

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == id);
        
        dbManifest.CanvasPaintings.Should().HaveCount(1);
        dbManifest.CanvasPaintings!.First().Label!.First().Value[0].Should().Be("canvas testing");
        // space added using the DLCS space
        dbManifest.CanvasPaintings!.First().AssetId.ToString().Should()
            .Be($"{Customer}/{NewlyCreatedSpace}/{assetId}");
        dbManifest.Batches.Should().HaveCount(2);
        dbManifest.Batches!.Last().Status.Should().Be(BatchStatus.Ingesting);
        dbManifest.Batches!.Last().Id.Should().Be(batchId);
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"staging/{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        s3Manifest.Items.Should().BeNull();
    }
    
    [Fact]
    public async Task UpdateManifest_CorrectlyUpdatesAssetRequests_RemovesSingleAsset()
    {
        // Arrange
        var (slug, id, assetId) = TestIdentifiers.SlugResourceAsset();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = "first",
                CanvasOrder = 1,
            },
            new()
            {
                Id = "second",
                CanvasOrder = 10,
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
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
                                    "canvasPainting":{
                                        "label": {
                                             "en": [
                                                 "canvas testing"
                                             ]
                                         }
                                    },
                                     "asset": {
                                         "id": "{{assetId}}",
                                         "batch": "{{batchId}}",
                                         "mediaType": "image/jpg"
                                     }
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.Id.Should().NotBeNull();
        responseManifest.Modified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        responseManifest.CreatedBy.Should().Be("Admin");
        responseManifest.Slug.Should().Be(slug);
        responseManifest.Parent.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == id);
        
        dbManifest.CanvasPaintings.Should().HaveCount(1);
        dbManifest.CanvasPaintings!.First().Label!.First().Value[0].Should().Be("canvas testing");
        // space added using the DLCS space
        dbManifest.CanvasPaintings!.First().AssetId.ToString().Should()
            .Be($"{Customer}/{NewlyCreatedSpace}/{assetId}");
        dbManifest.Batches.Should().HaveCount(2);
        dbManifest.Batches!.Last().Status.Should().Be(BatchStatus.Ingesting);
        dbManifest.Batches!.Last().Id.Should().Be(batchId);
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"staging/{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        s3Manifest.Items.Should().BeNull();
    }
    
    [Fact]
     public async Task UpdateManifest_CorrectlyUpdatesAssetRequests_WhenMultipleAssets()
     {
         // Arrange
         var (slug, id, assetId) = TestIdentifiers.SlugResourceAsset();

         await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: TestIdentifiers.BatchId(),
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
                                          "id": "{{assetId}}-0",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                      "asset": {
                                          "id": "{{assetId}}-1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                      "asset": {
                                          "id": "{{assetId}}-2",
                                          "mediaType": "image/jpg"
                                      }
                                  }
                              ] 
                          }
                          """;
         
         var requestMessage =
             HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", payload);
         etagManager.SetCorrectEtag(requestMessage, id, Customer);
         
         // Act
         var response = await httpClient.AsCustomer().SendAsync(requestMessage);

         // Assert
         response.StatusCode.Should().Be(HttpStatusCode.Accepted);
         var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

         responseManifest!.Id.Should().NotBeNull();
         
         var dbManifest = dbContext.Manifests
             .Include(m => m.CanvasPaintings)
             .Include(m => m.Batches)
             .First(x => x.Id == id);

         dbManifest.CanvasPaintings!.Should().HaveCount(3);
         var currentCanvasOrder = 0;
         dbManifest.Batches.Should().HaveCount(2);
         dbManifest.Batches!.Last().Status.Should().Be(BatchStatus.Ingesting);
         dbManifest.Batches!.Last().Id.Should().Be(batchId);
         
         foreach (var canvasPainting in dbManifest.CanvasPaintings!)
         {
             canvasPainting.CanvasOrder.Should().Be(currentCanvasOrder);
             canvasPainting.AssetId.ToString().Should()
                 .Be($"{Customer}/{NewlyCreatedSpace}/{assetId}-{currentCanvasOrder}");
             currentCanvasOrder++;
         }
     }
     
     [Fact]
     public async Task UpdateManifest_CorrectlyOrdersCanvasPaintings_WhenCanvasPaintingSetsOrder()
     {
         // Arrange
         var (slug, id, assetId) = TestIdentifiers.SlugResourceAsset();
        
         await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: TestIdentifiers.BatchId(), ingested: true);
         await dbContext.SaveChangesAsync();
         
         var batchId = TestIdentifiers.BatchId();
         var payload = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting":{
                                         "label": {
                                              "en": [
                                                  "canvas testing"
                                              ]
                                          },
                                          "canvasLabel": {
                                              "en": [
                                                  "canvas testing"
                                              ]
                                          },
                                        "canvasOrder": 2
                                     },
                                      "asset": {
                                          "id": "{{assetId}}-0",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "{{assetId}}-1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 0
                                     },
                                      "asset": {
                                          "id": "{{assetId}}-2",
                                          "mediaType": "image/jpg"
                                      }
                                  }
                              ] 
                          }
                          """;
         
         var requestMessage =
             HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", payload);
         etagManager.SetCorrectEtag(requestMessage, id, Customer);
         
         // Act
         var response = await httpClient.AsCustomer().SendAsync(requestMessage);

         // Assert
         response.StatusCode.Should().Be(HttpStatusCode.Accepted);
         var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

         responseManifest!.Id.Should().NotBeNull();

         var orderedCanvasPaintings = await dbContext.CanvasPaintings
             .Where(cp => cp.ManifestId == id)
             .OrderBy(cp => cp.CanvasOrder)
             .ToListAsync();
         
         orderedCanvasPaintings.Should().HaveCount(3);

         orderedCanvasPaintings[0].CanvasOrder.Should().Be(0);
         orderedCanvasPaintings[0].AssetId.ToString().Should().Be($"{Customer}/{NewlyCreatedSpace}/{assetId}-2");
         
         orderedCanvasPaintings[1].CanvasOrder.Should().Be(1);
         orderedCanvasPaintings[1].AssetId.ToString().Should().Be($"{Customer}/{NewlyCreatedSpace}/{assetId}-1");
         
         orderedCanvasPaintings[2].CanvasOrder.Should().Be(2);
         orderedCanvasPaintings[2].CanvasLabel.Should().NotBeNull();
         orderedCanvasPaintings[2].Label.Should().NotBeNull();
         orderedCanvasPaintings[2].AssetId.ToString().Should().Be($"{Customer}/{NewlyCreatedSpace}/{assetId}-0");
     }
     
     [Fact]
     public async Task UpdateManifest_CorrectlySetsChoiceOrder_WhenCanvasPaintingSetsChoice()
     {
         // Arrange
         var (slug, id, assetId) = TestIdentifiers.SlugResourceAsset();
        
         await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: TestIdentifiers.BatchId(), ingested: true);
         await dbContext.SaveChangesAsync();
         var batchId = TestIdentifiers.BatchId();
         
         var payload = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting":{
                                        "canvasOrder": 1,
                                        "choiceOrder": 1
                                     },
                                      "asset": {
                                          "id": "{{assetId}}-0",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 1,
                                          "choiceOrder": 2
                                     },
                                      "asset": {
                                          "id": "{{assetId}}-1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 0
                                     },
                                      "asset": {
                                          "id": "{{assetId}}-2",
                                          "mediaType": "image/jpg"
                                      }
                                  }
                              ] 
                          }
                          """;
         
         var requestMessage =
             HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", payload);
         etagManager.SetCorrectEtag(requestMessage, id, Customer);
         
         // Act
         var response = await httpClient.AsCustomer().SendAsync(requestMessage);

         // Assert
         response.StatusCode.Should().Be(HttpStatusCode.Accepted);
         var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

         responseManifest!.Id.Should().NotBeNull();

         var orderedCanvasPaintings = await dbContext.CanvasPaintings
             .Where(cp => cp.ManifestId == id)
             .OrderBy(cp => cp.CanvasOrder)
             .ThenBy(cp => cp.ChoiceOrder)
             .ToListAsync();
         
         orderedCanvasPaintings.Should().HaveCount(3);
         
         orderedCanvasPaintings[0].CanvasOrder.Should().Be(0);
         orderedCanvasPaintings[0].AssetId.ToString().Should().Be($"{Customer}/{NewlyCreatedSpace}/{assetId}-2");
         
         orderedCanvasPaintings[1].CanvasOrder.Should().Be(1);
         orderedCanvasPaintings[1].AssetId.ToString().Should().Be($"{Customer}/{NewlyCreatedSpace}/{assetId}-0");
         orderedCanvasPaintings[1].ChoiceOrder.Should().Be(1);
         orderedCanvasPaintings[2].CanvasOrder.Should().Be(1);
         orderedCanvasPaintings[2].AssetId.ToString().Should().Be($"{Customer}/{NewlyCreatedSpace}/{assetId}-1");
         orderedCanvasPaintings[2].ChoiceOrder.Should().Be(2);

         orderedCanvasPaintings[1].Id.Should().Be(orderedCanvasPaintings[2].Id,
             "CanvasPaintings that share canvasOrder have same canvasId");
     }
     
    [Fact]
    public async Task UpdateManifest_ReturnsError_WhenErrorFromDlcs()
    {
        // Arrange
        var (slug, id, assetId) = TestIdentifiers.SlugResourceAsset();
        
        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();
        
        A.CallTo(() => DLCSApiClient.IngestAssets(Customer,
            A<List<JObject>>.That.Matches(o => o.First().GetValue("id").ToString() == assetId),
            A<CancellationToken>._)).Throws(new DlcsException("DLCS exception", HttpStatusCode.BadRequest));
        
        var payload = $$"""
                         {
                             "type": "Manifest",
                             "slug": "{{slug}}",
                             "parent": "http://localhost/{{Customer}}/collections/root",
                             "paintedResources": [
                                {
                                     "asset": {
                                         "id": "{{assetId}}",
                                         "mediaType": "image/jpg",
                                     }
                                 }
                             ] 
                         }
                         """;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();
        errorResponse!.Detail.Should().Be("DLCS exception");
    }
    
    [Fact]
    public async Task UpdateManifest_CorrectlyUpdatesAssetRequests_WithSpaceFromAsset()
    {
        // Arrange
        var (slug, id, assetId) = TestIdentifiers.SlugResourceAsset();
        
        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();
        
        var space = 18;
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
                                         "space": {{space}},
                                     }
                                 }
                             ] 
                         }
                         """;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var canvasPainting = await dbContext.CanvasPaintings.SingleAsync(cp => cp.ManifestId == id);
        canvasPainting.AssetId.Space.Should().Be(space, "Space comes from the Asset");
    }
    
    [Fact]
    public async Task UpdateManifest_CreatesNewCanvasId_WhenUpdatingAsset_CanvasIdNotSpecified()
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

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
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
                                         "mediaType": "image/jpg"
                                     }
                                 },
                                 {
                                     "asset": {
                                         "id": "{{assetId}}_2",
                                         "batch": "{{batchId}}",
                                         "mediaType": "image/jpg"
                                     }
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.Id.Should().NotBeNull();
        
        var canvasPainting = await dbContext.CanvasPaintings.FirstAsync(cp => cp.ManifestId == id);
        canvasPainting.Id.Should().NotBe(canvasId, "New canvasId minted as no id provided");
        
        responseManifest.PaintedResources.First().CanvasPainting.CanvasId.Should()
            .Be($"http://localhost/1/canvases/{canvasPainting.Id}");
    }
    
    [Fact]
    public async Task UpdateManifest_MaintainsCanvasId_WhenUpdatingAsset_CanvasIdSpecified()
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

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();
        
        var initialCanvasPaintingId = initialCanvasPaintings[0].CanvasPaintingId;
        
        var batchId = TestIdentifiers.BatchId();
        var payload = $$"""
                         {
                             "type": "Manifest",
                             "slug": "{{slug}}",
                             "parent": "http://localhost/{{Customer}}/collections/root",
                             "paintedResources": [
                                 {
                                     "canvasPainting":{
                                          "canvasId": "{{canvasId}}"
                                     },
                                     "asset": {
                                         "id": "{{assetId}}",
                                         "mediaType": "image/jpg"
                                     }
                                 },
                                 {
                                     "canvasPainting":{
                                          "canvasId": "addToAvoidAllTrackedError"
                                     },
                                     "asset": {
                                         "id": "{{assetId}}_2",
                                         "batch": "{{batchId}}",
                                         "mediaType": "image/jpg"
                                     }
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.Id.Should().NotBeNull();

        var canvasPainting =
            await dbContext.CanvasPaintings.FirstAsync(cp => cp.ManifestId == id && cp.CanvasOrder == 0);
        canvasPainting.Id.Should().Be(canvasId, "CanvasId reused");
        canvasPainting.CanvasPaintingId.Should().Be(initialCanvasPaintingId, "DB row updated");
        
        responseManifest.PaintedResources.First().CanvasPainting.CanvasId.Should()
            .Be($"http://localhost/1/canvases/{canvasId}");
    }
    
    [Fact]
    public async Task UpdateManifest_CreatesNewCanvasId_WhenAddingAndUpdatingAssetWithoutCanvasPaintingBlock()
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

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
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
                                     }
                                 },
                                 {
                                     "asset": {
                                         "id": "{{assetId}}_newone",
                                         "batch": "{{batchId}}",
                                         "mediaType": "image/jpg"
                                     }
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var canvasPaintings = await dbContext.CanvasPaintings.Where(cp => cp.ManifestId == id).ToListAsync();
        canvasPaintings.Should().HaveCount(2);
        canvasPaintings.Should().NotContain(cp => cp.Id == canvasId);
    }
    
    [Fact]
    public async Task UpdateManifest_UpdatesAssetRequests_WithSpaceFromManifest()
    {
        // Arrange
        var (slug, id, assetId) = TestIdentifiers.SlugResourceAsset();
        var manifestSpace = 500;

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, spaceId: manifestSpace,
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
                                     }
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var canvasPainting = await dbContext.CanvasPaintings.SingleAsync(cp => cp.ManifestId == id);
        canvasPainting.AssetId.Space.Should().Be(manifestSpace, "Asset added using manifest space");
        
        A.CallTo(() => DLCSApiClient.CreateSpace(Customer, id, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }
    
    [Fact]
    public async Task UpdateManifest_CreatesNewCanvasId_WhenUpdatingChoiceOrder_NoCanvasIdSpecified()
    {
        // Arrange
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                ChoiceOrder = 2,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_2")
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
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
                                     "canvasPainting":{
                                        "canvasOrder": 1,
                                        "choiceOrder": 1
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 1,
                                          "choiceOrder": 2
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_2",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 2
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_3",
                                          "mediaType": "image/jpg",
                                          "batch": "{{batchId}}"
                                      }
                                  }
                              ] 
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(3);

        var orderedCanvasPaintings = await dbContext.CanvasPaintings
            .Where(cp => cp.ManifestId == id)
            .OrderBy(cp => cp.CanvasOrder)
            .ThenBy(cp => cp.ChoiceOrder)
            .ToListAsync();
        
        orderedCanvasPaintings[0].Id.Should().Be(orderedCanvasPaintings[1].Id, "Choices share canvasId");
        orderedCanvasPaintings[0].Id.Should().NotBe(canvasId);
        orderedCanvasPaintings[1].Id.Should().NotBe(canvasId);
    }
    
    [Fact]
    public async Task UpdateManifest_KeepsCanvasId_WhenUpdatingChoiceOrder_CanvasIdSpecified()
    {
        // Arrange
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                ChoiceOrder = 2,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_2")
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
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
                                     "canvasPainting":{
                                        "canvasId": "{{canvasId}}",
                                        "canvasOrder": 1,
                                        "choiceOrder": 1
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                        "canvasId": "{{canvasId}}",
                                        "canvasOrder": 1,
                                        "choiceOrder": 2
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_2",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                        "canvasId": "anotherCanvasId",
                                        "canvasOrder": 2
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_3",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg"
                                      }
                                  }
                              ] 
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(3);

        var orderedCanvasPaintings = await dbContext.CanvasPaintings
            .Where(cp => cp.ManifestId == id)
            .OrderBy(cp => cp.CanvasOrder)
            .ThenBy(cp => cp.ChoiceOrder)
            .ToListAsync();
        
        orderedCanvasPaintings[0].Id.Should().Be(orderedCanvasPaintings[1].Id, "Choices share canvasId");
        orderedCanvasPaintings[0].Id.Should().Be(canvasId);
        orderedCanvasPaintings[1].Id.Should().Be(canvasId);
    }
    
    [Fact]
    public async Task UpdateManifest_CreatesNewCanvasId_WhenAddingChoiceToCanvas_NoCanvasIdSpecified()
    {
        // Arrange
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
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
                                     "canvasPainting":{
                                        "canvasOrder": 1,
                                        "choiceOrder": 1
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 1,
                                          "choiceOrder": 2
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_2",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg"
                                      }
                                  }
                              ] 
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(2);

        var orderedCanvasPaintings = await dbContext.CanvasPaintings
            .Where(cp => cp.ManifestId == id)
            .OrderBy(cp => cp.CanvasOrder)
            .ThenBy(cp => cp.ChoiceOrder)
            .ToListAsync();
        
        orderedCanvasPaintings[0].Id.Should().Be(orderedCanvasPaintings[1].Id, "Choices share canvasId");
        orderedCanvasPaintings[0].Id.Should().NotBe(canvasId);
        orderedCanvasPaintings[1].Id.Should().NotBe(canvasId);
    }
    
    [Fact]
    public async Task UpdateManifest_KeepsCanvasId_WhenAddingChoiceToCanvas_CanvasIdSpecified()
    {
        // Arrange
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();

        var canvasPaintingId = initialCanvasPaintings.Select(c => c.CanvasPaintingId).Single();

        var batchId = TestIdentifiers.BatchId();
        var payload = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting":{
                                        "canvasId": "{{canvasId}}",
                                        "canvasOrder": 1,
                                        "choiceOrder": 1
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasId": "{{canvasId}}",
                                          "canvasOrder": 1,
                                          "choiceOrder": 2
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_2",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg"
                                      }
                                  }
                              ] 
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(2);

        var orderedCanvasPaintings = await dbContext.CanvasPaintings
            .Where(cp => cp.ManifestId == id)
            .OrderBy(cp => cp.CanvasOrder)
            .ThenBy(cp => cp.ChoiceOrder)
            .ToListAsync();
        
        orderedCanvasPaintings[0].Id.Should().Be(orderedCanvasPaintings[1].Id, "Choices share canvasId");
        orderedCanvasPaintings[0].Id.Should().Be(canvasId);
        orderedCanvasPaintings[1].Id.Should().Be(canvasId);

        orderedCanvasPaintings.Select(cp => cp.CanvasPaintingId).Should().Contain(canvasPaintingId, "DB row updated");
    }
    
    [Fact]
    public async Task UpdateManifest_CreatesNewCanvasId_WhenUpdatingCanvasToChoice()
    {
        // Arrange
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 2,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_2")
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
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
                                     "canvasPainting":{
                                        "canvasOrder": 1,
                                        "choiceOrder": 1
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 1,
                                          "choiceOrder": 2
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_2",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 2
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_3",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg"
                                      }
                                  }
                              ] 
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(3);
        
        var orderedCanvasPaintings = await dbContext.CanvasPaintings
            .Where(cp => cp.ManifestId == id)
            .OrderBy(cp => cp.CanvasOrder)
            .ThenBy(cp => cp.ChoiceOrder)
            .ToListAsync();

        orderedCanvasPaintings[0].Id.Should().Be(orderedCanvasPaintings[1].Id, "Choices share canvasId");
        orderedCanvasPaintings[0].Id.Should().NotBe(canvasId);
        orderedCanvasPaintings[0].CanvasOrder.Should().Be(1);
        orderedCanvasPaintings[0].ChoiceOrder.Should().Be(1);
        orderedCanvasPaintings[1].Id.Should().NotBe(canvasId);
        orderedCanvasPaintings[1].CanvasOrder.Should().Be(1);
        orderedCanvasPaintings[1].ChoiceOrder.Should().Be(2);
    }
    
    [Fact]
    public async Task UpdateManifest_SplittingChoiceOrder_ReturnsAccepted()
    {
        /*
         Test used to be a negative, now it presents how the split is implemented by removing and creating new non-choice
         canvases. To achieve that, the payload should omit canvasId for both canvases
        */
        
        // Arrange
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                ChoiceOrder = 2,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_2")
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
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
                                     "canvasPainting":{
                                        "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 2
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_2",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 3
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_3",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg"
                                      }
                                  }
                              ] 
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        responseManifest.PaintedResources.Should().HaveCount(3);

        var canvasOne = responseManifest.PaintedResources[0].CanvasPainting;
        var canvasTwo = responseManifest.PaintedResources[1].CanvasPainting;

        canvasOne.CanvasId.Should().NotBeEquivalentTo(canvasTwo.CanvasId);
        canvasOne.CanvasOrder.Should().Be(1);
        canvasTwo.CanvasOrder.Should().Be(2);
        
        var orderedCanvasPaintings = await dbContext.CanvasPaintings
            .Where(cp => cp.ManifestId == id)
            .OrderBy(cp => cp.CanvasOrder)
            .ToListAsync();
        orderedCanvasPaintings[0].Id.Should().NotBe(orderedCanvasPaintings[1].Id);
    }
    
    [Fact]
    public async Task UpdateManifest_BadRequest_WhenManifestWithoutBatchIsUpdatedWithAssets()
    {
        // Arrange
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug,
            canvasPaintings: [new CanvasPainting { Id = canvasId, CanvasOrder = 1, }]);
        await dbContext.SaveChangesAsync();

        var payload = $$"""
                         {
                             "type": "Manifest",
                             "slug": "{{slug}}",
                             "parent": "http://localhost/{{Customer}}/collections/root",
                             "paintedResources": [
                                 {
                                     "asset": {
                                         "id": "{{assetId}}",
                                         "mediaType": "image/jpg"
                                     }
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                payload);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        error.ErrorTypeUri.Should()
            .Be("http://localhost/errors/ModifyCollectionType/ManifestCreatedWithItemsCannotBeUpdatedWithAssets");
    }
    
    [Fact]
    public async Task UpdateManifest_UpdatesManifest_WhenExistingManifestNoItemsOrAssets()
    {
        // Arrange
        var (slug, id, assetId) = TestIdentifiers.SlugResourceAsset();
        
        await dbContext.Manifests.AddTestManifest(id: id, slug: slug);
        await dbContext.SaveChangesAsync();
        var batchId = TestIdentifiers.BatchId();

        var manifestWithoutSpace = $$"""
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
                                     }
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifestWithoutSpace);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
    
    [Fact]
    public async Task UpdateManifest_FindsAssetInDlcs_WhenMixOfNewAndOldAssets()
    {
        // Arrange
        var (slug, id, assetId) = TestIdentifiers.SlugResourceAsset();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = "first",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            }
        };

        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                A<ICollection<string>>.That.Matches(o =>
                    o.First().Split('/', StringSplitOptions.None).Last().StartsWith("fromDlcs_")),
                A<CancellationToken>._))
            .ReturnsLazily((int customerId, ICollection<string> assetIds, CancellationToken can) =>
                Task.FromResult((IList<JObject>)assetIds
                    .Where(a => a.Split('/', StringSplitOptions.None).Last().StartsWith("fromDlcs_"))
                    .Select(x => JObject.Parse($$"""
                                                 {
                                                   "id": "{{x.Split('/').Last()}}",
                                                   "space": {{NewlyCreatedSpace}}
                                                 }
                                                 """)).ToList()));

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();
        
        var batchId = TestIdentifiers.BatchId();

        var manifestWithoutSpace = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting":{
                                        "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 2
                                     },
                                      "asset": {
                                          "id": "fromDlcs_{{assetId}}_2",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 3
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_3",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg",
                                      }
                                  }
                              ] 
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifestWithoutSpace);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(3);
        
        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 1).Should().NotBeNull("asset added to manifest");
        dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 2).Should().NotBeNull("asset added to manifest");
        dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 3).Should().NotBeNull("asset added to manifest");
        
        A.CallTo(() => DLCSApiClient.UpdateAssetManifest(Customer,
            A<List<string>>.That.Matches(a => a.Single() == $"{Customer}/{NewlyCreatedSpace}/fromDlcs_{assetId}_2"), 
            A<OperationType>.That.Matches(a => a == OperationType.Add),
            A<List<string>>._, A<CancellationToken>._)).MustHaveHappened();
        
        dbManifest.Batches.Should().HaveCount(2, "batch created for asset 3");
    }

    [Fact]
    public async Task UpdateManifest_RemoveItemFromExistingCanvas_MultipleImageComposition()
    {
        // Initial state - 3 CanvasPaintings all on same canvas, non choice. Remove one.
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        var batchId = TestIdentifiers.BatchId();
        
        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                CanvasOrder = 0,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-1"),
                Target = "xywh=0,0,200,200",
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-2"),
                Target = "xywh=200,200,200,200",
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 2,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-3"),
            }
        };
        
        List<CanvasPainting> expected =
        [
            new()
            {
                Id = canvasId,
                CanvasOrder = 0,
                Label = new LanguageMap("en", "Top Left"),
                Target = "xywh=0,0,200,200",
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-1"),
                Ingesting = true,
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-3"),
                Ingesting = true,
            },
            new()
            {
                Id = "addedToAvoidAllTrackedError",
                CanvasOrder = 2,
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-4"),
                Ingesting = true,
            }
        ];

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();
        
        var manifest = $$"""
                           {
                               "type": "Manifest",
                               "slug": "{{slug}}",
                               "parent": "http://localhost/{{Customer}}/collections/root",
                               "paintedResources": [
                                   {
                                       "canvasPainting": {
                                           "canvasId": "{{canvasId}}",
                                           "target": "xywh=0,0,200,200",
                                           "label": {"en": ["Top Left"]}
                                       },
                                       "asset": {
                                           "id": "{{assetId}}-1"
                                       }
                                   },
                                   {
                                       "canvasPainting": {
                                           "canvasId": "{{canvasId}}",
                                       },
                                       "asset": {
                                           "id": "{{assetId}}-3"
                                       }
                                   },
                                   {
                                       "canvasPainting": {
                                           "canvasId": "addedToAvoidAllTrackedError",
                                       },
                                       "asset": {
                                           "id": "{{assetId}}-4",
                                           "batch": "{{batchId}}",
                                       }
                                   }
                               ]
                           }
                           """;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifest);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        var canvasPaintings = dbContext.CanvasPaintings
            .Where(x => x.ManifestId == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last())
            .ToList();

        canvasPaintings.Should().BeEquivalentTo(expected,
            cfg => cfg.Excluding(cp => cp.CanvasPaintingId)
                .Excluding(cp => cp.ManifestId)
                .Excluding(cp => cp.Modified)
                .Excluding(cp => cp.Created));
    }
    
    [Fact]
    public async Task UpdateManifest_AddItemToExistingCanvas_MultipleImageComposition()
    {
        // Initial state - 2 CanvasPaintings all on same canvas, non choice. Add one.
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        var batchId = TestIdentifiers.BatchId();
        
        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                CanvasOrder = 0,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-1"),
                Target = "xywh=0,0,200,200",
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-3"),
            }
        };
        
        List<CanvasPainting> expected =
        [
            new()
            {
                Id = canvasId,
                CanvasOrder = 0,
                Label = new LanguageMap("en", "Top Left"),
                Target = "xywh=0,0,200,200",
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-1"),
                Ingesting = true,
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                Label = new LanguageMap("en", "Bottom Right"),
                Target = "xywh=800,800,100,50",
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-2"),
                Ingesting = true,
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 2,
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-3"),
                Ingesting = true,
            }
        ];

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();
        
        var manifest = $$"""
                           {
                               "type": "Manifest",
                               "slug": "{{slug}}",
                               "parent": "http://localhost/{{Customer}}/collections/root",
                               "paintedResources": [
                                   {
                                       "canvasPainting": {
                                           "canvasId": "{{canvasId}}",
                                           "target": "xywh=0,0,200,200",
                                           "label": {"en": ["Top Left"]}
                                       },
                                       "asset": {
                                           "id": "{{assetId}}-1"
                                       }
                                   },
                                   {
                                       "canvasPainting": {
                                           "canvasId": "{{canvasId}}",
                                           "target": "xywh=800,800,100,50",
                                           "label": {"en": ["Bottom Right"]}
                                       },
                                       "asset": {
                                           "id": "{{assetId}}-2",
                                           "batch": "{{batchId}}",
                                       }
                                   },
                                   {
                                       "canvasPainting": {
                                           "canvasId": "{{canvasId}}",
                                       },
                                       "asset": {
                                           "id": "{{assetId}}-3"
                                       }
                                   }
                               ]
                           }
                           """;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifest);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        var canvasPaintings = dbContext.CanvasPaintings
            .Where(x => x.ManifestId == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last())
            .ToList();

        canvasPaintings.Should().BeEquivalentTo(expected,
            cfg => cfg.Excluding(cp => cp.CanvasPaintingId)
                .Excluding(cp => cp.ManifestId)
                .Excluding(cp => cp.Modified)
                .Excluding(cp => cp.Created));
    }
    
    [Fact]
    public async Task UpdateManifest_AddChoiceToExistingCanvas_MultipleImageComposition()
    {
        // Initial state - 1 CanvasPaintings on canvas, add a second CanvasPainting which is a choice
        var (slug, id, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        var batchId = TestIdentifiers.BatchId();
        
        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                CanvasOrder = 0,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-1"),
            },
        };
        
        List<CanvasPainting> expected =
        [
            new()
            {
                Id = canvasId,
                CanvasOrder = 0,
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-1"),
                Ingesting = true,
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-2"),
                Ingesting = true,
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                ChoiceOrder = 2,
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-3"),
                Ingesting = true,
            }
        ];

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();
        
        var manifest = $$"""
                           {
                               "type": "Manifest",
                               "slug": "{{slug}}",
                               "parent": "http://localhost/{{Customer}}/collections/root",
                               "paintedResources": [
                                   {
                                       "canvasPainting": {
                                           "canvasId": "{{canvasId}}",
                                           "canvasOrder": 0
                                       },
                                       "asset": {
                                           "id": "{{assetId}}-1"
                                       }
                                   },
                                   {
                                       "canvasPainting": {
                                           "canvasId": "{{canvasId}}",
                                           "canvasOrder": 1,
                                           "choiceOrder": 1
                                       },
                                       "asset": {
                                           "id": "{{assetId}}-2",
                                           "batch": "{{batchId}}",
                                       }
                                   },
                                   {
                                       "canvasPainting": {
                                           "canvasId": "{{canvasId}}",
                                           "canvasOrder": 1,
                                           "choiceOrder": 2
                                       },
                                       "asset": {
                                           "id": "{{assetId}}-3"
                                       }
                                   }
                               ]
                           }
                           """;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifest);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        var canvasPaintings = dbContext.CanvasPaintings
            .Where(x => x.ManifestId == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last())
            .ToList();

        canvasPaintings.Should().BeEquivalentTo(expected,
            cfg => cfg.Excluding(cp => cp.CanvasPaintingId)
                .Excluding(cp => cp.ManifestId)
                .Excluding(cp => cp.Modified)
                .Excluding(cp => cp.Created));
    }
    
    [Fact]
    public async Task UpdateManifest_SameAssetIdDifferentSpaces_WhenMixOfNewAndOldAssets()
    {
        // Arrange
        var (slug, id, assetId) = TestIdentifiers.SlugResourceAsset();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = "first",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            },
            new()
            {
                Id = "second",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 2,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_2")
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();

        var firstCanvasPaintingId = dbContext.CanvasPaintings.First(cp => cp.ManifestId == id && cp.CanvasOrder == 1)
            .CanvasPaintingId;
        var secondCanvasPaintingId = dbContext.CanvasPaintings.First(cp => cp.ManifestId == id && cp.CanvasOrder == 2)
            .CanvasPaintingId;
        
        var batchId = TestIdentifiers.BatchId();
        var manifestWithoutSpace = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting":{
                                        "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 2
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_2",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 3
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_3",
                                          "mediaType": "image/jpg",
                                          "batch": "{{batchId}}",
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 4
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_3",
                                          "space": 3,
                                          "mediaType": "image/jpg"
                                      }
                                  }
                              ] 
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifestWithoutSpace);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(4);
        
        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());
        
        var newAssetManifestSpace = dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 3);
        var newAssetAssetSpace = dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 4);
        
        newAssetManifestSpace.AssetId.Should().Be(AssetId.FromString($"{Customer}/{NewlyCreatedSpace}/{assetId}_3"));
        newAssetAssetSpace.AssetId.Should().Be(AssetId.FromString($"{Customer}/3/{assetId}_3"));
        
        A.CallTo(() => DLCSApiClient.IngestAssets(Customer,
            A<List<JObject>>.That.Matches(o => o.First().GetValue("id").ToString() == "returnNothing"),
            A<CancellationToken>._)).MustNotHaveHappened();
        
        dbManifest.Batches.Should().HaveCount(2);
        dbManifest.Batches.Single(b => b.Id == batchId).Should().NotBeNull("Assets not found, so batch created");
    }
    
    [Fact]
    public async Task UpdateManifest_RemovesAssetFromManifest_WhenTrackedAssetRemoved()
    {
        // Arrange
        var slug = nameof(UpdateManifest_RemovesAssetFromManifest_WhenTrackedAssetRemoved);
        var id = $"{nameof(UpdateManifest_RemovesAssetFromManifest_WhenTrackedAssetRemoved)}_id";
        var assetId = "testAssetByPresentation-only-calls-new";

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = "first",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();

        var firstCanvasPaintingId = dbContext.CanvasPaintings.First(cp => cp.ManifestId == id && cp.CanvasOrder == 1)
            .CanvasPaintingId;

        var batchId = TestIdentifiers.BatchId();

        var manifestWithoutSpace = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting":{
                                          "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "{{assetId}}_2",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg",
                                      }
                                  }
                              ] 
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifestWithoutSpace);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(1);
        
        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 1).CanvasPaintingId.Should().NotBe(firstCanvasPaintingId);
        
        dbManifest.Batches.Should().HaveCount(2, "batch created for asset 2");
        
        A.CallTo(() => DLCSApiClient.UpdateAssetManifest(Customer,
            A<List<string>>.That.Matches(a => a.Single() == $"{Customer}/{NewlyCreatedSpace}/{assetId}_1"),
            A<OperationType>.That.Matches(a => a == OperationType.Remove),
            A<List<string>>._, A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task UpdateManifest_BadRequest_WhenAssetsMatchedFromAnotherManifest()
    {
        // THIS TEST WILL FAIL ONCE #352 IS IMPLEMENTED
        
        // Arrange
        var slug = nameof(UpdateManifest_BadRequest_WhenAssetsMatchedFromAnotherManifest);
        var id = $"{nameof(UpdateManifest_BadRequest_WhenAssetsMatchedFromAnotherManifest)}_id";
        var assetId = "testAssetByPresentation-update-assetInAnotherManifest";
        
        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: 920, ingested: true);
        await dbContext.Manifests.AddTestManifest(id: $"{id}_anotherManifest", slug: $"{slug}_another_slug", ingested: true, canvasPaintings:
        [
            new CanvasPainting
            {
                Id = "first",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, assetId)
            }
        ]);
        await dbContext.SaveChangesAsync();
        
        var batchId = TestIdentifiers.BatchId();
        var manifestWithoutSpace = $$"""
                         {
                             "type": "Manifest",
                             "slug": "{{slug}}",
                             "parent": "http://localhost/{{Customer}}/collections/root",
                             "paintedResources": [
                                 {
                                    "canvasPainting":{
                                        "label": {
                                             "en": [
                                                 "canvas testing"
                                             ]
                                         }
                                    },
                                     "asset": {
                                         "id": "{{assetId}}",
                                         "batch": "{{batchId}}",
                                         "mediaType": "image/jpg"
                                     }
                                 }
                             ] 
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifestWithoutSpace);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError, "The asset is picked up from another manifest, which means that all assets are tracked");
    }
}

