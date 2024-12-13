using System.Net;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using DLCS.API;
using DLCS.Models;
using FakeItEasy;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.General;
using Newtonsoft.Json.Linq;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using Batch = DLCS.Models.Batch;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyManifestAssetCreationTests : IClassFixture<PresentationAppFactory<Program>>, IClassFixture<StorageFixture>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private const int Customer = 1;
    private readonly IAmazonS3 amazonS3;
    private const int NewlyCreatedSpace = 999;
    private readonly IDlcsApiClient dlcsApiClient;

    public ModifyManifestAssetCreationTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
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
        
        dbContext = storageFixture.DbFixture.DbContext;

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services => services.AddSingleton(dlcsApiClient));

        storageFixture.DbFixture.CleanUp();
    }
    
    [Fact]
    public async Task CreateManifest_BadRequest_WhenNoSpaceHeader()
    {
        // Arrange
        var slug = nameof(CreateManifest_BadRequest_WhenNoSpaceHeader);
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            PaintedResources = new List<PaintedResource>()
            {
                new ()
                {
                    CanvasPainting = new CanvasPainting()
                    {
                        CanvasId = "https://iiif.example/manifest.json"
                    },
                    Asset = new JObject()
                }
            }
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        error!.Detail.Should().Be("A request with assets requires the space header to be set");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/RequiresSpace");
    }
    
    [Fact]
    public async Task CreateManifest_CorrectlyCreatesAssetRequests_WithSpace()
    {
        // Arrange
        var slug = nameof(CreateManifest_CorrectlyCreatesAssetRequests_WithSpace);
        var space = 18;
        var assetId = "testAssetByPresentation-withSpace";
        var batchId = 1;
        var manifestWithSpace = $$"""
                         {
                             "type": "Manifest",
                             "behavior": [
                                 "public-iiif"
                             ],
                             "label": {
                                 "en": [
                                     "post testing"
                                 ]
                             },
                             "slug": "{{slug}}",
                             "parent": "root",
                             "thumbnail": [
                                 {
                                     "id": "https://example.org/img/thumb.jpg",
                                     "type": "Image",
                                     "format": "image/jpeg",
                                     "width": 300,
                                     "height": 200
                                 }
                             ],
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
                                         "string1": "somestring",
                                         "string2": "somestring2",
                                         "string3": "somestring3",
                                         "origin": "some/origin",
                                         "deliveryChannels": [
                                             {
                                                 "channel": "iiif-img",
                                                 "policy": "default"
                                             },
                                             {
                                                 "channel": "thumbs",
                                                 "policy": "default"
                                             }
                                         ]
                                     }
                                 }
                             ] 
                         }
                         """;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifestWithSpace);
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.Id.Should().NotBeNull();
        responseManifest.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
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
        dbManifest.CanvasPaintings!.First().AssetId.Should().Be($"{Customer}/{space}/{assetId}");
        dbManifest.Batches.Should().HaveCount(1);
        dbManifest.Batches!.First().Status.Should().Be(BatchStatus.Ingesting);
        dbManifest.Batches!.First().Id.Should().Be(batchId);
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        s3Manifest.Items.Should().BeNull();
    }
    
    [Fact]
    public async Task CreateManifest_CorrectlyCreatesAssetRequests_WithoutSpace()
    {
        // Arrange
        var slug = nameof(CreateManifest_CorrectlyCreatesAssetRequests_WithoutSpace);
        var assetId = "testAssetByPresentation-withoutSpace";
        var batchId = 2;
        var manifestWithoutSpace = $$"""
                         {
                             "type": "Manifest",
                             "behavior": [
                                 "public-iiif"
                             ],
                             "label": {
                                 "en": [
                                     "post testing"
                                 ]
                             },
                             "slug": "{{slug}}",
                             "parent": "root",
                             "thumbnail": [
                                 {
                                     "id": "https://example.org/img/thumb.jpg",
                                     "type": "Image",
                                     "format": "image/jpeg",
                                     "width": 300,
                                     "height": 200
                                 }
                             ],
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
                                         "string1": "somestring",
                                         "string2": "somestring2",
                                         "string3": "somestring3",
                                         "origin": "some/origin",
                                         "deliveryChannels": [
                                             {
                                                 "channel": "iiif-img",
                                                 "policy": "default"
                                             },
                                             {
                                                 "channel": "thumbs",
                                                 "policy": "default"
                                             }
                                         ]
                                     }
                                 }
                             ] 
                         }
                         """;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifestWithoutSpace);
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.Id.Should().NotBeNull();
        responseManifest.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
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
        // space added using the manifest space
        dbManifest.CanvasPaintings!.First().AssetId.Should()
            .Be($"{Customer}/{NewlyCreatedSpace}/{assetId}");
        dbManifest.Batches.Should().HaveCount(1);
        dbManifest.Batches!.First().Status.Should().Be(BatchStatus.Ingesting);
        dbManifest.Batches!.First().Id.Should().Be(batchId);
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        s3Manifest.Items.Should().BeNull();
    }
    
        [Fact]
    public async Task? CreateManifest_BadRequest_WheCalledWithoutCanvasPainting()
    {
        // Arrange
        var slug = nameof(CreateManifest_BadRequest_WheCalledWithoutCanvasPainting);
        var manifestWithoutSpace = $$"""
                         {
                             "type": "Manifest",
                             "behavior": [
                                 "public-iiif"
                             ],
                             "label": {
                                 "en": [
                                     "post testing"
                                 ]
                             },
                             "slug": "{{slug}}",
                             "parent": "root",
                             "thumbnail": [
                                 {
                                     "id": "https://example.org/img/thumb.jpg",
                                     "type": "Image",
                                     "format": "image/jpeg",
                                     "width": 300,
                                     "height": 200
                                 }
                             ],
                             "paintedResources": [
                                {
                                     "asset": {
                                         "id": "testAssetByPresentation",
                                         "mediaType": "image/jpg",
                                         "string1": "somestring",
                                         "string2": "somestring2",
                                         "string3": "somestring3",
                                         "origin": "some/origin",
                                         "deliveryChannels": [
                                             {
                                                 "channel": "iiif-img",
                                                 "policy": "default"
                                             },
                                             {
                                                 "channel": "thumbs",
                                                 "policy": "default"
                                             }
                                         ]
                                     }
                                 }
                             ] 
                         }
                         """;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifestWithoutSpace);
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseError = await response.ReadAsPresentationResponseAsync<Error>();
        responseError!.Detail.Should().Be("A canvas painting element is required in a painted resource block");
        
        var dbRecords = dbContext.Hierarchy.Where(x =>
            x.Slug == slug);
        dbRecords.Count().Should().Be(0);
    }
    
    [Fact]
    public async Task CreateManifest_BadRequest_WhenCalledWithEmptyAsset()
    {
        // Arrange
        var slug = nameof(CreateManifest_BadRequest_WhenCalledWithEmptyAsset);
        var manifestWithoutSpace = $$"""
                         {
                             "type": "Manifest",
                             "behavior": [
                                 "public-iiif"
                             ],
                             "label": {
                                 "en": [
                                     "post testing"
                                 ]
                             },
                             "slug": "{{slug}}",
                             "parent": "root",
                             "thumbnail": [
                                 {
                                     "id": "https://example.org/img/thumb.jpg",
                                     "type": "Image",
                                     "format": "image/jpeg",
                                     "width": 300,
                                     "height": 200
                                 }
                             ],
                             "paintedResources": [
                                 {
                                     "canvasPainting": {
                                     },
                                     "asset": {
                                     }
                                 }
                             ] 
                         }
                         """;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifestWithoutSpace);
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseError = await response.ReadAsPresentationResponseAsync<Error>();
        responseError!.Detail.Should().Be("Could not retrieve an id from an attached asset");
        
        var dbRecords = dbContext.Hierarchy.Where(x =>
            x.Slug == slug);
        dbRecords.Count().Should().Be(0);
    }
    
     [Fact]
     public async Task CreateManifest_CorrectlyCreatesAssetRequests_WhenMultipleAssets()
     {
         // Arrange
         var slug = nameof(CreateManifest_CorrectlyCreatesAssetRequests_WhenMultipleAssets);
         var batchId = 3;
         
         var manifestWithoutSpace = $$"""
                          {
                              "type": "Manifest",
                              "behavior": [
                                  "public-iiif"
                              ],
                              "label": {
                                  "en": [
                                      "post testing"
                                  ]
                              },
                              "slug": "{{slug}}",
                              "parent": "root",
                              "thumbnail": [
                                  {
                                      "id": "https://example.org/img/thumb.jpg",
                                      "type": "Image",
                                      "format": "image/jpeg",
                                      "width": 300,
                                      "height": 200
                                  }
                              ],
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
                                          "id": "testAssetByPresentation-multipleAssets-1",
                                          "batch": "{{batchId}}",
                                          "mediaType": "image/jpg",
                                          "string1": "somestring",
                                          "string2": "somestring2",
                                          "string3": "somestring3",
                                          "origin": "some/origin",
                                          "deliveryChannels": [
                                              {
                                                  "channel": "iiif-img",
                                                  "policy": "default"
                                              },
                                              {
                                                  "channel": "thumbs",
                                                  "policy": "default"
                                              }
                                          ]
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
                                          "id": "testAssetByPresentation-multipleAssets-2",
                                          "mediaType": "image/jpg",
                                          "string1": "somestring",
                                          "string2": "somestring2",
                                          "string3": "somestring3",
                                          "origin": "some/origin",
                                          "deliveryChannels": [
                                              {
                                                  "channel": "iiif-img",
                                                  "policy": "default"
                                              },
                                              {
                                                  "channel": "thumbs",
                                                  "policy": "default"
                                              }
                                          ]
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
                                          "id": "testAssetByPresentation-multipleAssets-3",
                                          "mediaType": "image/jpg",
                                          "string1": "somestring",
                                          "string2": "somestring2",
                                          "string3": "somestring3",
                                          "origin": "some/origin",
                                          "deliveryChannels": [
                                              {
                                                  "channel": "iiif-img",
                                                  "policy": "default"
                                              },
                                              {
                                                  "channel": "thumbs",
                                                  "policy": "default"
                                              }
                                          ]
                                      }
                                  }
                              ] 
                          }
                          """;
         
         var requestMessage =
             HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifestWithoutSpace);
         requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");
         
         // Act
         var response = await httpClient.AsCustomer().SendAsync(requestMessage);

         // Assert
         response.StatusCode.Should().Be(HttpStatusCode.Created);
         var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

         responseManifest!.Id.Should().NotBeNull();
         
         var dbManifest = dbContext.Manifests
             .Include(m => m.CanvasPaintings)
             .Include(m => m.Batches)
             .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

         dbManifest.CanvasPaintings!.Count().Should().Be(3);
         var currentCanvasOrder = 1;
         dbManifest.Batches.Should().HaveCount(1);
         dbManifest.Batches!.First().Status.Should().Be(BatchStatus.Ingesting);
         dbManifest.Batches!.First().Id.Should().Be(batchId);
         
         foreach (var canvasPainting in dbManifest.CanvasPaintings!)
         {
             canvasPainting.CanvasOrder.Should().Be(currentCanvasOrder);
             canvasPainting.AssetId.Should()
                 .Be($"{Customer}/{NewlyCreatedSpace}/testAssetByPresentation-multipleAssets-{currentCanvasOrder}");
             currentCanvasOrder++;
         }
     }
}
