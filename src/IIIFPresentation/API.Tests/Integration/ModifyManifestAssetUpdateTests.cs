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
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.General;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using Batch = DLCS.Models.Batch;

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
    private readonly IDlcsApiClient dlcsApiClient;
    private readonly IETagManager etagManager;
    
    public ModifyManifestAssetUpdateTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        dlcsApiClient = A.Fake<IDlcsApiClient>();
        
        A.CallTo(() => dlcsApiClient.CreateSpace(Customer, A<string>._, A<CancellationToken>._))
            .Returns(new Space { Id = NewlyCreatedSpace, Name = "test" });
        
        A.CallTo(() => dlcsApiClient.IngestAssets(Customer, A<List<JObject>>._, A<CancellationToken>._))
            .ReturnsLazily(x => Task.FromResult(
                new List<Batch> { new ()
                {
                    ResourceId =  x.Arguments.Get<List<JObject>>("images").First().GetValue("batch").ToString(), 
                    Submitted = DateTime.Now
                }}));
        
        A.CallTo(() => dlcsApiClient.IngestAssets(Customer,
            A<List<JObject>>.That.Matches(o => o.First().GetValue("id").ToString() == "returnError"),
            A<CancellationToken>._)).Throws(new DlcsException("DLCS exception", HttpStatusCode.BadRequest));
        
        dbContext = storageFixture.DbFixture.DbContext;

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services => services.AddSingleton(dlcsApiClient));
        
        etagManager = (IETagManager)factory.Services.GetRequiredService(typeof(IETagManager));

        storageFixture.DbFixture.CleanUp();
    }
    
    [Fact]
    public async Task UpdateManifest_CorrectlyUpdatesAssetRequests_AddsSingleAsset()
    {
        // Arrange
        var slug = nameof(UpdateManifest_CorrectlyUpdatesAssetRequests_AddsSingleAsset);
        var id = $"{nameof(UpdateManifest_CorrectlyUpdatesAssetRequests_AddsSingleAsset)}_id";
        
        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: 901, ingested: true);
        await dbContext.SaveChangesAsync();

        var assetId = "testAssetByPresentation-update";
        var batchId = 1001;
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
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());
        
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
        var slug = nameof(UpdateManifest_CorrectlyUpdatesAssetRequests_RemovesSingleAsset);
        var id = $"{nameof(UpdateManifest_CorrectlyUpdatesAssetRequests_RemovesSingleAsset)}_id";

        var initialCanvasPaintings = new List<Models.Database.CanvasPainting>
        {
            new()
            {
                Id = "first",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1
            },
            new()
            {
                Id = "second",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 10,
                ChoiceOrder = 1
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: 902, ingested: true);
        await dbContext.SaveChangesAsync();

        var assetId = "testAssetByPresentation-update";
        var batchId = 1002;
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
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());
        
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
         var slug = nameof(UpdateManifest_CorrectlyUpdatesAssetRequests_WhenMultipleAssets);
         var id = $"{nameof(UpdateManifest_CorrectlyUpdatesAssetRequests_WhenMultipleAssets)}_id";
        
         await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: 903, ingested: true);
         await dbContext.SaveChangesAsync();
         var batchId = 1003;
         
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
                                          "id": "testAssetByPresentation-update-multipleAssets-0",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                         "label": {
                                              "en": [
                                                  "canvas testing"
                                              ]
                                          }
                                     },
                                      "asset": {
                                          "id": "testAssetByPresentation-update-multipleAssets-1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                         "label": {
                                              "en": [
                                                  "canvas testing"
                                              ]
                                          }
                                     },
                                      "asset": {
                                          "id": "testAssetByPresentation-update-multipleAssets-2",
                                          "mediaType": "image/jpg"
                                      }
                                  }
                              ] 
                          }
                          """;
         
         var requestMessage =
             HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifestWithoutSpace);
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
             .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

         dbManifest.CanvasPaintings!.Count().Should().Be(3);
         var currentCanvasOrder = 0;
         dbManifest.Batches.Should().HaveCount(2);
         dbManifest.Batches!.Last().Status.Should().Be(BatchStatus.Ingesting);
         dbManifest.Batches!.Last().Id.Should().Be(batchId);
         
         foreach (var canvasPainting in dbManifest.CanvasPaintings!)
         {
             canvasPainting.CanvasOrder.Should().Be(currentCanvasOrder);
             canvasPainting.AssetId.ToString().Should()
                 .Be($"{Customer}/{NewlyCreatedSpace}/testAssetByPresentation-update-multipleAssets-{currentCanvasOrder}");
             currentCanvasOrder++;
         }
     }
     
     [Fact]
     public async Task UpdateManifest_CorrectlyOrdersCanvasPaintings_WhenCanvasPaintingSetsOrder()
     {
         // Arrange
         var slug = nameof(UpdateManifest_CorrectlyOrdersCanvasPaintings_WhenCanvasPaintingSetsOrder);
         var id = $"{nameof(UpdateManifest_CorrectlyOrdersCanvasPaintings_WhenCanvasPaintingSetsOrder)}_id";
        
         await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: 904, ingested: true);
         await dbContext.SaveChangesAsync();
         
         var batchId = 1004;
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
                                          },
                                          "canvasLabel": {
                                              "en": [
                                                  "canvas testing"
                                              ]
                                          },
                                        "canvasOrder": 2
                                     },
                                      "asset": {
                                          "id": "testAssetByPresentation-multipleAssets-0",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                         "label": {
                                              "en": [
                                                  "canvas testing"
                                              ]
                                          },
                                          "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "testAssetByPresentation-multipleAssets-1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                         "label": {
                                              "en": [
                                                  "canvas testing"
                                              ]
                                          },
                                          "canvasOrder": 0
                                     },
                                      "asset": {
                                          "id": "testAssetByPresentation-multipleAssets-2",
                                          "mediaType": "image/jpg"
                                      }
                                  }
                              ] 
                          }
                          """;
         
         var requestMessage =
             HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifestWithoutSpace);
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
             .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

         dbManifest.CanvasPaintings!.Count().Should().Be(3);
         dbManifest.Batches.Should().HaveCount(2);
         dbManifest.Batches!.Last().Status.Should().Be(BatchStatus.Ingesting);
         dbManifest.Batches!.Last().Id.Should().Be(batchId);
         
         dbManifest.CanvasPaintings[0].CanvasOrder.Should().Be(2);
         dbManifest.CanvasPaintings[0].CanvasLabel.Should().NotBeNull();
         dbManifest.CanvasPaintings[0].Label.Should().NotBeNull();
         dbManifest.CanvasPaintings[0].AssetId.ToString().Should()
             .Be($"{Customer}/{NewlyCreatedSpace}/testAssetByPresentation-multipleAssets-0");
         dbManifest.CanvasPaintings[1].CanvasOrder.Should().Be(1);
         dbManifest.CanvasPaintings[1].AssetId.ToString().Should()
             .Be($"{Customer}/{NewlyCreatedSpace}/testAssetByPresentation-multipleAssets-1");
         dbManifest.CanvasPaintings[2].CanvasOrder.Should().Be(0);
         dbManifest.CanvasPaintings[2].AssetId.ToString().Should()
             .Be($"{Customer}/{NewlyCreatedSpace}/testAssetByPresentation-multipleAssets-2");
     }
     
     [Fact]
     public async Task UpdateManifest_CorrectlySetsChoiceOrder_WhenCanvasPaintingSetsChoice()
     {
         // Arrange
         var slug = nameof(UpdateManifest_CorrectlySetsChoiceOrder_WhenCanvasPaintingSetsChoice);
         var id = $"{nameof(UpdateManifest_CorrectlySetsChoiceOrder_WhenCanvasPaintingSetsChoice)}_id";
        
         await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: 906, ingested: true);
         await dbContext.SaveChangesAsync();
         var batchId = 1006;
         
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
                                          },
                                        "canvasOrder": 1,
                                        "choiceOrder": 1
                                     },
                                      "asset": {
                                          "id": "testAssetByPresentation-multipleAssets-0",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                         "label": {
                                              "en": [
                                                  "canvas testing"
                                              ]
                                          },
                                          "canvasOrder": 1,
                                          "choiceOrder": 2
                                     },
                                      "asset": {
                                          "id": "testAssetByPresentation-multipleAssets-1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                         "label": {
                                              "en": [
                                                  "canvas testing"
                                              ]
                                          },
                                          "canvasOrder": 0
                                     },
                                      "asset": {
                                          "id": "testAssetByPresentation-multipleAssets-2",
                                          "mediaType": "image/jpg"
                                      }
                                  }
                              ] 
                          }
                          """;
         
         var requestMessage =
             HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifestWithoutSpace);
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
             .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

         dbManifest.CanvasPaintings!.Should().HaveCount(3);
         dbManifest.Batches.Should().HaveCount(2);
         dbManifest.Batches!.Last().Status.Should().Be(BatchStatus.Ingesting);
         dbManifest.Batches!.Last().Id.Should().Be(batchId);
         
         dbManifest.CanvasPaintings[0].CanvasOrder.Should().Be(1);
         dbManifest.CanvasPaintings[0].AssetId.ToString().Should()
             .Be($"{Customer}/{NewlyCreatedSpace}/testAssetByPresentation-multipleAssets-0");
         dbManifest.CanvasPaintings[0].ChoiceOrder.Should().Be(1);
         dbManifest.CanvasPaintings[1].CanvasOrder.Should().Be(1);
         dbManifest.CanvasPaintings[1].AssetId.ToString().Should()
             .Be($"{Customer}/{NewlyCreatedSpace}/testAssetByPresentation-multipleAssets-1");
         dbManifest.CanvasPaintings[1].ChoiceOrder.Should().Be(2);

         dbManifest.CanvasPaintings[0].Id.Should().Be(dbManifest.CanvasPaintings[1].Id,
             "CanvasPaintings that share canvasOrder have same canvasId");
         
         dbManifest.CanvasPaintings[2].CanvasOrder.Should().Be(0);
         dbManifest.CanvasPaintings[2].AssetId.ToString().Should()
             .Be($"{Customer}/{NewlyCreatedSpace}/testAssetByPresentation-multipleAssets-2");
     }
     
    [Fact]
    public async Task? UpdateManifest_ReturnsError_WhenErrorFromDlcs()
    {
        // Arrange
        var slug = nameof(UpdateManifest_ReturnsError_WhenErrorFromDlcs);
        var id = $"{nameof(UpdateManifest_ReturnsError_WhenErrorFromDlcs)}_id";
        
        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: 907, ingested: true);
        await dbContext.SaveChangesAsync();
        var batchId = 1007;
        var assetId = "returnError";
        
        var manifestWithoutSpace = $$"""
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
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifestWithoutSpace);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();
        errorResponse!.Detail.Should().Be("DLCS exception");
    }
    
    [Fact]
    public async Task UpdateManifest_CorrectlyUpdatesAssetRequests_WithSpace()
    {
        // Arrange
        var slug = nameof(UpdateManifest_CorrectlyUpdatesAssetRequests_WithSpace);
        var id = $"{nameof(UpdateManifest_CorrectlyUpdatesAssetRequests_WithSpace)}_id";
        
        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, batchId: 908, ingested: true);
        await dbContext.SaveChangesAsync();
        
        var space = 18;
        var assetId = "testAssetByPresentation-withSpace";
        var batchId = 1008;
        var manifestWithSpace = $$"""
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
                                         "mediaType": "image/jpg",
                                         "space": {{space}},
                                     }
                                 }
                             ] 
                         }
                         """;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifestWithSpace);
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

        var dbManifest = dbContext.Manifests.Include(m => m.CanvasPaintings)
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x =>
            x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());
        
        dbManifest.CanvasPaintings.Should().HaveCount(1);
        dbManifest.CanvasPaintings!.First().Label!.First().Value[0].Should().Be("canvas testing");
        // space comes from the asset
        dbManifest.CanvasPaintings!.First().AssetId.ToString().Should().Be($"{Customer}/{space}/{assetId}");
        dbManifest.Batches.Should().HaveCount(2);
        dbManifest.Batches!.Last().Status.Should().Be(BatchStatus.Ingesting);
        dbManifest.Batches!.Last().Id.Should().Be(batchId);
        dbManifest.LastProcessed.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"staging/{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        s3Manifest.Items.Should().BeNull();
    }
    
    [Fact]
    public async Task UpdateManifest_KeepsTheSameCanvasId_WhenUpdatingAsset()
    {
        // Arrange
        var slug = nameof(UpdateManifest_KeepsTheSameCanvasId_WhenUpdatingAsset);
        var id = $"{nameof(UpdateManifest_KeepsTheSameCanvasId_WhenUpdatingAsset)}_id";
        var assetId = "testAssetByPresentation-update";
        var canvasId = "first";

        var initialCanvasPaintings = new List<Models.Database.CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, assetId)
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: 909, ingested: true);
        await dbContext.SaveChangesAsync();
        
        var initialCanvasPaintingId = dbContext.CanvasPaintings.First(cp => cp.ManifestId == id).CanvasPaintingId;
        
        var batchId = 1009;
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
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.Id.Should().NotBeNull();
        responseManifest.PaintedResources.First().CanvasPainting.CanvasId.Should().Be($"http://localhost/1/canvases/{canvasId}");

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());
        
        dbManifest.CanvasPaintings.Should().HaveCount(1);
        dbManifest.CanvasPaintings!.First().Id.Should().Be(canvasId);
        dbManifest.CanvasPaintings!.First().CanvasPaintingId.Should().Be(initialCanvasPaintingId);
    }
    
    [Fact]
    public async Task UpdateManifest_KeepsTheSameCanvasId_WhenAddingAndUpdatingAssetWithoutCanvasPaintingBlock()
    {
        // Arrange
        var slug = nameof(UpdateManifest_KeepsTheSameCanvasId_WhenAddingAndUpdatingAssetWithoutCanvasPaintingBlock);
        var id = $"{nameof(UpdateManifest_KeepsTheSameCanvasId_WhenAddingAndUpdatingAssetWithoutCanvasPaintingBlock)}_id";
        var assetId = "testAssetByPresentation-update";
        var canvasId = "first";

        var initialCanvasPaintings = new List<Models.Database.CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, assetId)
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: 910, ingested: true);
        await dbContext.SaveChangesAsync();
        
        var batchId = 1010;
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
                manifestWithoutSpace);
        etagManager.SetCorrectEtag(requestMessage, id, Customer);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should()
            .Contain(pr => pr.CanvasPainting!.CanvasId == $"http://localhost/1/canvases/{canvasId}");
    }
    
    [Fact]
    public async Task UpdateManifest_UpdatesAssetRequests_WithSpaceFromManifest()
    {
        // Arrange
        var slug = nameof(UpdateManifest_UpdatesAssetRequests_WithSpaceFromManifest);
        var id = $"{nameof(UpdateManifest_UpdatesAssetRequests_WithSpaceFromManifest)}_id";
        var space = 500;
        
        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, spaceId: space, batchId: 911, ingested: true);
        await dbContext.SaveChangesAsync();

        var assetId = "testAssetByPresentation-update-keepSpace";
        var batchId = 1011;
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
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());
        
        dbManifest.CanvasPaintings.Should().HaveCount(1);
        // space added using the manifest space
        dbManifest.CanvasPaintings!.First().AssetId.ToString().Should()
            .Be($"{Customer}/{space}/{assetId}");

        A.CallTo(() => dlcsApiClient.CreateSpace(A<int>.Ignored, A<string>.Ignored, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }
    
    [Fact]
    public async Task UpdateManifest_KeepsTheSameCanvasPaintingId_WhenUpdatingChoiceOrder()
    {
        // Arrange
        var slug = nameof(UpdateManifest_KeepsTheSameCanvasPaintingId_WhenUpdatingChoiceOrder);
        var id = $"{nameof(UpdateManifest_KeepsTheSameCanvasPaintingId_WhenUpdatingChoiceOrder)}_id";
        var assetId = "testAssetByPresentation-update-choice";
        var canvasId = "first";

        var initialCanvasPaintings = new List<Models.Database.CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            },
            new()
            {
                Id = canvasId,
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 2,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_2")
            }
            
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: 912, ingested: true);
        await dbContext.SaveChangesAsync();

        var firstCanvasPaintingId = dbContext.CanvasPaintings.First(cp => cp.ManifestId == id && cp.ChoiceOrder == 1)
            .CanvasPaintingId;
        var secondCanvasPaintingId = dbContext.CanvasPaintings.First(cp => cp.ManifestId == id && cp.ChoiceOrder == 2)
            .CanvasPaintingId;
        
        var batchId = 1012;
        var manifestWithoutSpace = $$"""
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
                                          "id": "{{assetId}}_2",
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

        responseManifest!.PaintedResources.Count.Should().Be(2);
        
        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings.First(cp => cp.Id == canvasId && cp.ChoiceOrder == 1).CanvasPaintingId.Should().Be(firstCanvasPaintingId);
        dbManifest.CanvasPaintings.First(cp => cp.Id == canvasId && cp.ChoiceOrder == 2).CanvasPaintingId.Should().Be(secondCanvasPaintingId);
    }
    
    [Fact]
    public async Task UpdateManifest_KeepsTheSameCanvasPaintingId_WhenAddingCanvasToChoiceOrder()
    {
        // Arrange
        var slug = nameof(UpdateManifest_KeepsTheSameCanvasPaintingId_WhenAddingCanvasToChoiceOrder);
        var id = $"{nameof(UpdateManifest_KeepsTheSameCanvasPaintingId_WhenAddingCanvasToChoiceOrder)}_id";
        var assetId = "testAssetByPresentation-add-choice";
        var canvasId = "first";

        var initialCanvasPaintings = new List<Models.Database.CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: 913, ingested: true);
        await dbContext.SaveChangesAsync();

        var firstCanvasPaintingId = dbContext.CanvasPaintings.First(cp => cp.ManifestId == id && cp.ChoiceOrder == 1)
            .CanvasPaintingId;
        
        var batchId = 1013;
        var manifestWithoutSpace = $$"""
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
                                          "id": "{{assetId}}_2",
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

        responseManifest!.PaintedResources.Count.Should().Be(2);
        
        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings.First(cp => cp.Id == canvasId && cp.ChoiceOrder == 1).CanvasPaintingId.Should().Be(firstCanvasPaintingId);
    }
    
    [Fact]
    public async Task UpdateManifest_KeepsTheSameCanvasPaintingId_WhenUpdatingCanvasToChoiceOrder()
    {
        // Arrange
        var slug = nameof(UpdateManifest_KeepsTheSameCanvasPaintingId_WhenUpdatingCanvasToChoiceOrder);
        var id = $"{nameof(UpdateManifest_KeepsTheSameCanvasPaintingId_WhenUpdatingCanvasToChoiceOrder)}_id";
        var assetId = "testAssetByPresentation-update-canvas-choice";
        var canvasId = "first";

        var initialCanvasPaintings = new List<Models.Database.CanvasPainting>
        {
            new()
            {
                Id = canvasId,
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
                CanvasOrder = 1,
                ChoiceOrder = 2,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_2")
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: 914, ingested: true);
        await dbContext.SaveChangesAsync();

        var firstCanvasPaintingId = dbContext.CanvasPaintings.First(cp => cp.ManifestId == id && cp.ChoiceOrder == 1)
            .CanvasPaintingId;
        
        var batchId = 1014;
        var manifestWithoutSpace = $$"""
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
                                          "id": "{{assetId}}_2",
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

        responseManifest!.PaintedResources.Count.Should().Be(2);
        
        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings.First(cp => cp.Id == canvasId && cp.ChoiceOrder == 1).CanvasPaintingId.Should().Be(firstCanvasPaintingId);
        dbManifest.CanvasPaintings.FirstOrDefault(cp => cp.Id == canvasId && cp.ChoiceOrder == 2).Should().NotBeNull();
    }
    
    [Fact]
    public async Task UpdateManifest_ThrowsError_SplittingChoiceOrder()
    {
        /*
         THIS TEST SHOULD FAIL WHEN ACTUALLY WORKING
        
         this demonstrates the behavior as-is for splitting a choice order. In reality this should work and not throw an exception
        */
        
        // Arrange
        var slug = nameof(UpdateManifest_ThrowsError_SplittingChoiceOrder);
        var id = $"{nameof(UpdateManifest_ThrowsError_SplittingChoiceOrder)}_id";
        var assetId = "testAssetByPresentation-update-canvas-choice";
        var canvasId = "first";

        var initialCanvasPaintings = new List<Models.Database.CanvasPainting>
        {
            new()
            {
                Id = canvasId,
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            },
            new()
            {
                Id = canvasId,
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 2,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_2")
            }
        };

        await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: 915, ingested: true);
        await dbContext.SaveChangesAsync();
        
        var batchId = 1015;
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
                                          "batch": "{{batchId}}",
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();

        errorResponse!.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/DuplicateCanvasId");
    }
    
    [Fact]
    public async Task UpdateManifest_BadRequest_WhenManifestWithBatchIsUpdatedWithAssets()
    {
        // Arrange
        var slug = nameof(UpdateManifest_BadRequest_WhenManifestWithBatchIsUpdatedWithAssets);
        var id = $"{nameof(UpdateManifest_BadRequest_WhenManifestWithBatchIsUpdatedWithAssets)}_id";
        
        await dbContext.Manifests.AddTestManifest(id: id, slug: slug);
        await dbContext.SaveChangesAsync();

        var assetId = "testAssetByPresentation-update";
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        error.ErrorTypeUri.Should()
            .Be("http://localhost/errors/ModifyCollectionType/ManifestCreatedWithAssetsCannotBeUpdatedWithItems");
    }
}
