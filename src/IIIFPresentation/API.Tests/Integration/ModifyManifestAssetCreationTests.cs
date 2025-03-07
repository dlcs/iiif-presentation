using System.Net;
using Amazon.S3;
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

        A.CallTo(() => dlcsApiClient.IngestAssets(Customer,
            A<List<JObject>>.That.Matches(o => o.First().GetValue("id").ToString() == "returnError"),
            A<CancellationToken>._)).Throws(new DlcsException("DLCS exception", HttpStatusCode.BadRequest));

        A.CallTo(() => dlcsApiClient.GetCustomerImages(Customer,
                A<IList<string>>.That.Matches(l => l.Any(x =>
                    $"{Customer}/{NewlyCreatedSpace}/testAssetByPresentation-assetDetailsMissing".Equals(x))),
                A<CancellationToken>._))
            .ReturnsLazily(() => []);

        A.CallTo(() => dlcsApiClient.GetCustomerImages(Customer,
                A<IList<string>>.That.Matches(l => l.Any(x =>
                    $"{Customer}/{NewlyCreatedSpace}/testAssetByPresentation-assetDetailsFail".Equals(x))),
                A<CancellationToken>._))
            .Throws(new DlcsException("DLCS exception", HttpStatusCode.BadRequest));

        A.CallTo(() => dlcsApiClient.GetCustomerImages(Customer,
                A<IList<string>>.That.Matches(l =>
                    l.Any(x => $"{Customer}/{NewlyCreatedSpace}/testAssetByPresentation-assetDetails".Equals(x))),
                A<CancellationToken>._))
            .ReturnsLazily(() =>
            [
                JObject.Parse(
                    """
                    {
                           "@context": "https://localhost/contexts/Image.jsonld",
                           "@id": "https://localhost:7230/customers/1/spaces/999/images/testAssetByPresentation-assetDetails",
                           "@type": "vocab:Image",
                           "id": "testAssetByPresentation-assetDetails",
                           "space": 15,
                           "imageService": "https://localhost/iiif-img/1/15/testAssetByPresentation-assetDetails",
                           "thumbnailImageService": "https://localhost/thumbs/1/15/testAssetByPresentation-assetDetails",
                           "created": "2025-01-20T15:54:43.290925Z",
                           "origin": "https://example.com/photos/example.jpg",
                           "maxUnauthorised": -1,
                           "duration": 0,
                           "width": 0,
                           "height": 0,
                           "ingesting": true,
                           "error": "",
                           "tags": [],
                           "string1": "",
                           "string2": "",
                           "string3": "",
                           "number1": 0,
                           "number2": 0,
                           "number3": 0,
                           "roles": [],
                           "batch": "https://localhost/customers/1/queue/batches/2137",
                           "metadata": "https://localhost/customers/1/spaces/15/images/testAssetByPresentation-assetDetails/metadata",
                           "storage": "https://localhost/customers/1/spaces/15/images/testAssetByPresentation-assetDetails/storage",
                           "mediaType": "image/jpeg",
                           "family": "I",
                           "deliveryChannels": [
                             {
                               "@type": "vocab:DeliveryChannel",
                               "channel": "iiif-img",
                               "policy": "default"
                             },
                             {
                               "@type": "vocab:DeliveryChannel",
                               "channel": "thumbs",
                               "policy": "https://localhost/customers/1/deliveryChannelPolicies/thumbs/default"
                             }
                           ]
                         }
                    """
                )
            ]);
        
        dbContext = storageFixture.DbFixture.DbContext;

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services => services.AddSingleton(dlcsApiClient));

        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task CreateManifest_CreateSpace_ForSpacelessAssets_WhenNoSpaceHeader()
    {
        const string postedAssetId = "theAssetId";
        // Arrange
        var slug = nameof(CreateManifest_CreateSpace_ForSpacelessAssets_WhenNoSpaceHeader);
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
                        CanvasId = $"https://iiif.example/{Customer}/canvases/canvasId"
                    },
                    Asset = new(new JProperty("id", postedAssetId), new JProperty("batch", 123))
                }
            }
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, response.Headers.Location!.ToString());
        response = await httpClient.AsCustomer().SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        responseManifest.Should().NotBeNull();
        responseManifest!.PaintedResources.Should().NotBeNull();
        responseManifest.PaintedResources!.Count.Should().Be(1);
        responseManifest.PaintedResources.Single().Asset!.TryGetValue("@id", out var assetId).Should().BeTrue();
        assetId!.Type.Should().Be(JTokenType.String);
        assetId!.Value<string>().Should()
            .EndWith($"/customers/{Customer}/spaces/{NewlyCreatedSpace}/images/{postedAssetId}");
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
                             "parent": "http://localhost/{{Customer}}/collections/root",
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
        dbManifest.CanvasPaintings!.First().AssetId.ToString().Should().Be($"{Customer}/{space}/{assetId}");
        dbManifest.Batches.Should().HaveCount(1);
        dbManifest.Batches!.First().Status.Should().Be(BatchStatus.Ingesting);
        dbManifest.Batches!.First().Id.Should().Be(batchId);
        dbManifest.LastProcessed.Should().BeNull();
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StagingStorageBucketName,
                $"{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        s3Manifest.Items.Should().BeNull();
    }

    [Fact]
    public async Task CreateManifest_ReturnsErrorAsset_IfGetAllImagesFails()
    {
        // Arrange
        var slug = nameof(CreateManifest_ReturnsErrorAsset_IfGetAllImagesFails);
        var space = 15;
        var assetId = "testAssetByPresentation-assetDetailsFail";
        var batchId = 500;
        var manifestWithSpace =
            $$"""
              {
                  "type": "Manifest",
                  "parent": "http://localhost/{{Customer}}/collections/root",
                  "slug": "{{slug}}",
                  "rights": "https://creativecommons.org/licenses/by/4.0/",
                  "label": {
                      "en": [
                          "I have assets"
                      ]
                  },
                  "paintedResources": [
                      {
                          "asset": {
                              "id": "{{assetId}}",
                              "batch": {{batchId}},
                              "origin": "https://example.com/photos/example.jpg",
                              "mediaType": "image/jpeg"
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

        responseManifest.PaintedResources.Should().NotBeNull();
        responseManifest.PaintedResources.Should().HaveCount(1);
        responseManifest.PaintedResources!.Single().Asset.Should().NotBeNull();
        responseManifest.PaintedResources!.Single().Asset!.GetValue("error")!.Value<string>().Should()
            .Be("Unable to retrieve asset details");
    }

    [Fact]
    public async Task CreateManifest_ReturnsErrorAsset_IfGetAllImagesMissing()
    {
        // Arrange
        var slug = nameof(CreateManifest_ReturnsErrorAsset_IfGetAllImagesMissing);
        var space = 15;
        var assetId = "testAssetByPresentation-assetDetailsMissing";
        var batchId = 404;
        var manifestWithSpace =
            $$"""
              {
                  "type": "Manifest",
                  "parent": "http://localhost/{{Customer}}/collections/root",
                  "slug": "{{slug}}",
                  "rights": "https://creativecommons.org/licenses/by/4.0/",
                  "label": {
                      "en": [
                          "I have assets"
                      ]
                  },
                  "paintedResources": [
                      {
                          "asset": {
                              "id": "{{assetId}}",
                              "batch": {{batchId}},
                              "origin": "https://example.com/photos/example.jpg",
                              "mediaType": "image/jpeg"
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

        responseManifest.PaintedResources.Should().NotBeNull();
        responseManifest.PaintedResources.Should().HaveCount(1);
        responseManifest.PaintedResources!.Single().Asset.Should().NotBeNull();
        responseManifest.PaintedResources!.Single().Asset!.GetValue("error")!.Value<string>().Should()
            .Be("Asset not found");
    }

    [Fact]
    public async Task CreateManifest_ReturnsAssetDetails_FromAllImages()
    {
        // Arrange
        var slug = nameof(CreateManifest_ReturnsAssetDetails_FromAllImages);
        var space = 15;
        var assetId = "testAssetByPresentation-assetDetails";
        var batchId = 2137;
        var manifestWithSpace =
            $$"""
              {
                  "type": "Manifest",
                  "parent": "http://localhost/{{Customer}}/collections/root",
                  "slug": "{{slug}}",
                  "rights": "https://creativecommons.org/licenses/by/4.0/",
                  "label": {
                      "en": [
                          "I have assets"
                      ]
                  },
                  "paintedResources": [
                      {
                          "asset": {
                              "id": "{{assetId}}",
                              "batch": {{batchId}},
                              "origin": "https://example.com/photos/example.jpg",
                              "mediaType": "image/jpeg"
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
        responseManifest.Ingesting.Should().BeEquivalentTo(new IngestingAssets
        {
            Total = 1,
            Errors = 0,
            Finished = 0
        });

        responseManifest.PaintedResources.Should().NotBeNull();
        responseManifest.PaintedResources.Should().HaveCount(1);
        responseManifest.PaintedResources!.Single().Asset.Should().NotBeNull();
        responseManifest.PaintedResources!.Single().Asset!.GetValue("batch")!.Value<string>().Should()
            .Be("https://localhost/customers/1/queue/batches/2137");
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
                             "parent": "http://localhost/{{Customer}}/collections/root",
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
        dbManifest.CanvasPaintings!.First().AssetId.ToString().Should()
            .Be($"{Customer}/{NewlyCreatedSpace}/{assetId}");
        dbManifest.Batches.Should().HaveCount(1);
        dbManifest.Batches!.First().Status.Should().Be(BatchStatus.Ingesting);
        dbManifest.Batches!.First().Id.Should().Be(batchId);
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StagingStorageBucketName,
                $"{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        s3Manifest.Items.Should().BeNull();
    }
    
    [Fact]
    public async Task? CreateManifest_AllowsManifestCreation_WhenCalledWithoutCanvasPainting()
    {
        // Arrange
        var slug = nameof(CreateManifest_AllowsManifestCreation_WhenCalledWithoutCanvasPainting);
        var assetId = "assetWithoutCanvasPainting";
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
                             "parent": "http://localhost/{{Customer}}/collections/root",
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
        dbManifest.CanvasPaintings!.First().Label?.First().Should().BeNull();
        // space added using the manifest space
        dbManifest.CanvasPaintings!.First().AssetId.ToString().Should()
            .Be($"{Customer}/{NewlyCreatedSpace}/{assetId}");
        dbManifest.Batches.Should().HaveCount(1);
        dbManifest.Batches!.First().Status.Should().Be(BatchStatus.Ingesting);
        dbManifest.Batches!.First().Id.Should().Be(batchId);
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StagingStorageBucketName,
                $"{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        s3Manifest.Items.Should().BeNull();
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
                             "parent": "http://localhost/{{Customer}}/collections/root",
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
         var batchId = 4;
         
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
                              "parent": "http://localhost/{{Customer}}/collections/root",
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
                                          "id": "testAssetByPresentation-multipleAssets-0",
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
                                          "id": "testAssetByPresentation-multipleAssets-1",
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
         var currentCanvasOrder = 0;
         dbManifest.Batches.Should().HaveCount(1);
         dbManifest.Batches!.First().Status.Should().Be(BatchStatus.Ingesting);
         dbManifest.Batches!.First().Id.Should().Be(batchId);
         
         foreach (var canvasPainting in dbManifest.CanvasPaintings!)
         {
             canvasPainting.CanvasOrder.Should().Be(currentCanvasOrder);
             canvasPainting.AssetId.ToString().Should()
                 .Be($"{Customer}/{NewlyCreatedSpace}/testAssetByPresentation-multipleAssets-{currentCanvasOrder}");
             currentCanvasOrder++;
         }
     }
     
     [Fact]
     public async Task CreateManifest_CorrectlyOrdersAssetRequests_WhenCanvasPaintingSetsOrder()
     {
         // Arrange
         var slug = nameof(CreateManifest_CorrectlyOrdersAssetRequests_WhenCanvasPaintingSetsOrder);
         var batchId = 5;
         
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
                              "parent": "http://localhost/{{Customer}}/collections/root",
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
                                          },
                                          "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "testAssetByPresentation-multipleAssets-1",
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
                                          },
                                          "canvasOrder": 0
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
         dbManifest.Batches.Should().HaveCount(1);
         dbManifest.Batches!.First().Status.Should().Be(BatchStatus.Ingesting);
         dbManifest.Batches!.First().Id.Should().Be(batchId);
         
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
     public async Task CreateManifest_CorrectlySetsChoiceOrder_WhenCanvasPaintingSetsChoice()
     {
         // Arrange
         var slug = nameof(CreateManifest_CorrectlySetsChoiceOrder_WhenCanvasPaintingSetsChoice);
         var batchId = 6;
         
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
                              "parent": "http://localhost/{{Customer}}/collections/root",
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
                                          },
                                        "canvasOrder": 1,
                                        "choiceOrder": 1
                                     },
                                      "asset": {
                                          "id": "testAssetByPresentation-multipleAssets-0",
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
                                          },
                                          "canvasOrder": 1,
                                          "choiceOrder": 2
                                     },
                                      "asset": {
                                          "id": "testAssetByPresentation-multipleAssets-1",
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
                                          },
                                          "canvasOrder": 0
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

         dbManifest.CanvasPaintings!.Should().HaveCount(3);
         dbManifest.Batches.Should().HaveCount(1);
         dbManifest.Batches!.First().Status.Should().Be(BatchStatus.Ingesting);
         dbManifest.Batches!.First().Id.Should().Be(batchId);
         
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
    public async Task? CreateManifest_ReturnsError_WhenErrorFromDlcs()
    {
        // Arrange
        var slug = nameof(CreateManifest_AllowsManifestCreation_WhenCalledWithoutCanvasPainting);
        var assetId = "returnError";
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
                             "parent": "http://localhost/{{Customer}}/collections/root",
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
                                         "id": "{{assetId}}",
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
        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();
        errorResponse!.Detail.Should().Be("DLCS exception");
    }
}
