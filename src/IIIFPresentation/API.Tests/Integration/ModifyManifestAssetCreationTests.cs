using System.Net;
using Amazon.S3;
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
using DBCanvasPainting = Models.Database.CanvasPainting;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyManifestAssetCreationTests : IClassFixture<PresentationAppFactory<Program>>,
    IClassFixture<StorageFixture>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private const int Customer = 1;
    private readonly IAmazonS3 amazonS3;
    private const int NewlyCreatedSpace = 999;
    private static readonly IDlcsApiClient DLCSApiClient = A.Fake<IDlcsApiClient>();

    public ModifyManifestAssetCreationTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

        // Always return Space 999 when call to create space
        A.CallTo(() => DLCSApiClient.CreateSpace(Customer, A<string>._, A<CancellationToken>._))
            .Returns(new Space { Id = NewlyCreatedSpace, Name = "test" });

        // Echo back "batch" value set in first Asset
        A.CallTo(() => DLCSApiClient.IngestAssets(Customer, A<List<JObject>>._, A<CancellationToken>._))
            .ReturnsLazily(x => Task.FromResult(
                new List<Batch>
                {
                    new()
                    {
                        ResourceId = x.Arguments.Get<List<JObject>>("images").First().GetValue("batch").ToString(),
                        Submitted = DateTime.Now
                    }
                }));

        dbContext = storageFixture.DbFixture.DbContext;

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services => services.AddSingleton(DLCSApiClient));

        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task CreateManifest_CreateSpace_ForSpacelessAssets_WhenNoSpaceHeader()
    {
        // Arrange
        var (slug, assetId) = TestIdentifiers.SlugResource();
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            PaintedResources = new List<PaintedResource>()
            {
                new()
                {
                    CanvasPainting = new CanvasPainting()
                    {
                        CanvasId = $"https://iiif.example/{Customer}/canvases/canvasId"
                    },
                    Asset = new(new JProperty("id", assetId), new JProperty("batch", TestIdentifiers.BatchId()))
                }
            }
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.Location.Should().NotBeNull();

        requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, response.Headers.Location!.ToString());
        response = await httpClient.AsCustomer().SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        responseManifest.Should().NotBeNull();
        responseManifest!.PaintedResources.Should().NotBeNull();
        responseManifest.PaintedResources!.Count.Should().Be(1);
        responseManifest.PaintedResources.Single().Asset!.TryGetValue("@id", out var asset).Should().BeTrue();
        asset!.Type.Should().Be(JTokenType.String);
        asset!.Value<string>().Should()
            .EndWith($"/customers/{Customer}/spaces/{NewlyCreatedSpace}/images/{assetId}");
    }

    [Fact]
    public async Task CreateManifest_CorrectlyCreatesAssetRequests_WithSpace()
    {
        // Arrange
        var (slug, assetId) = TestIdentifiers.SlugResource();
        var space = 18;
        var batchId = TestIdentifiers.BatchId();
        var manifestWithSpace = $$"""
                                  {
                                      "type": "Manifest",
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
                                                  "origin": "some/origin"
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
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
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
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"staging/{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        s3Manifest.Items.Should().BeNull();
    }

    [Fact]
    public async Task CreateManifest_ReturnsErrorAsset_IfGetAllImagesFails()
    {
        // Arrange
        var (slug, assetId) = TestIdentifiers.SlugResource();
        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                A<IList<string>>.That.Matches(l => l.Any(x =>
                    $"{Customer}/{NewlyCreatedSpace}/{assetId}".Equals(x))),
                A<CancellationToken>._))
            .Throws(new DlcsException("DLCS exception", HttpStatusCode.BadRequest));

        var batchId = TestIdentifiers.BatchId();
        var manifestWithSpace =
            $$"""
              {
                  "type": "Manifest",
                  "parent": "http://localhost/{{Customer}}/collections/root",
                  "slug": "{{slug}}",
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
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
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
        var (slug, assetId) = TestIdentifiers.SlugResource();

        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                A<IList<string>>.That.Matches(l => l.Any(x =>
                    $"{Customer}/{NewlyCreatedSpace}/{assetId}".Equals(x))),
                A<CancellationToken>._))
            .ReturnsLazily(() => []);
        var batchId = TestIdentifiers.BatchId();
        var manifestWithSpace =
            $$"""
              {
                  "type": "Manifest",
                  "parent": "http://localhost/{{Customer}}/collections/root",
                  "slug": "{{slug}}",
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
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
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
        var (slug, _, assetId) = TestIdentifiers.SlugResourceAsset();

          A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                  A<IList<string>>.That.Matches(l =>
                      l.Any(x => $"{Customer}/{NewlyCreatedSpace}/{assetId}".Equals(x))),
                  A<CancellationToken>._)).ReturnsLazily(() => new List<JObject>()).Once().Then
              .ReturnsLazily(() =>
              [
                  JObject.Parse($$"""
                                  {
                                      "@id": "https://localhost:7230/customers/1/spaces/999/images/{{assetId}}",
                                      "id": "{{assetId}}",
                                      "space": 999,
                                      "batch": "https://localhost/customers/1/queue/batches/2137"
                                  }
                                  """
                  )
              ]);
          
        var manifestWithSpace =
            $$"""
              {
                  "type": "Manifest",
                  "parent": "http://localhost/{{Customer}}/collections/root",
                  "slug": "{{slug}}",
                  "paintedResources": [
                      {
                          "asset": {
                              "id": "{{assetId}}",
                              "batch": {{TestIdentifiers.BatchId()}},
                              "origin": "https://example.com/photos/example.jpg",
                              "mediaType": "image/jpeg"
                          }
                      }
                  ]
              }
              """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifestWithSpace);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
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
        responseManifest.PaintedResources!.First().Asset.Should().NotBeNull();
        responseManifest.PaintedResources!.First().Asset!.GetValue("batch")!.Value<string>().Should()
            .Be("https://localhost/customers/1/queue/batches/2137");
    }

    [Fact]
    public async Task CreateManifest_CorrectlyCreatesAssetRequests_WithoutSpace()
    {
        // Arrange
        var (slug, assetId) = TestIdentifiers.SlugResource();
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
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifestWithoutSpace);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
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
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"staging/{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        s3Manifest.Items.Should().BeNull();
    }

    [Fact]
    public async Task? CreateManifest_AllowsManifestCreation_WhenCalledWithoutCanvasPainting()
    {
        // Arrange
        var (slug, assetId) = TestIdentifiers.SlugResource();
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
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifestWithoutSpace);
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
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
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"staging/{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        s3Manifest.Items.Should().BeNull();
    }

    [Fact]
    public async Task CreateManifest_BadRequest_WhenCalledWithEmptyAsset()
    {
        // Arrange
        var slug = TestIdentifiers.Id();
        var manifestWithoutSpace = $$"""
                                     {
                                         "type": "Manifest",
                                         "slug": "{{slug}}",
                                         "parent": "http://localhost/{{Customer}}/collections/root",
                                         "paintedResources": [
                                             {
                                                 "canvasPainting": { },
                                                 "asset": { }
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
        var slug = TestIdentifiers.Id();
        var batchId = TestIdentifiers.BatchId();

        var manifestWithoutSpace = $$"""
                                     {
                                         "type": "Manifest",
                                         "slug": "{{slug}}",
                                         "parent": "http://localhost/{{Customer}}/collections/root",
                                         "paintedResources": [
                                             {
                                                 "asset": {
                                                     "id": "testAssetByPresentation-multipleAssets-0",
                                                     "batch": "{{batchId}}",
                                                     "mediaType": "image/jpg"
                                                 }
                                             },
                                             {
                                                 "asset": {
                                                     "id": "testAssetByPresentation-multipleAssets-1",
                                                     "mediaType": "image/jpg",
                                                     "origin": "some/origin"
                                                 }
                                             },
                                             {
                                                "asset": {
                                                     "id": "testAssetByPresentation-multipleAssets-2",
                                                     "mediaType": "image/jpg",
                                                     "origin": "some/origin"
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
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.Id.Should().NotBeNull();

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings!.Should().HaveCount(3);
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
        var slug = TestIdentifiers.Id();
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
                                                     "origin": "some/origin"
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
                                                     "origin": "some/origin"
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
                                                     "origin": "some/origin"
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
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
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
        var slug = TestIdentifiers.Id();
        var batchId = TestIdentifiers.BatchId();

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
                                                     "id": "testAssetByPresentation-multipleAssets-0",
                                                     "batch": "{{batchId}}",
                                                     "mediaType": "image/jpg",
                                                     "origin": "some/origin"
                                                 }
                                             },
                                             {
                                                "canvasPainting":{
                                                     "canvasOrder": 1,
                                                     "choiceOrder": 2
                                                },
                                                 "asset": {
                                                     "id": "testAssetByPresentation-multipleAssets-1",
                                                     "mediaType": "image/jpg",
                                                     "origin": "some/origin"
                                                 }
                                             },
                                             {
                                                "canvasPainting":{
                                                     "canvasOrder": 0
                                                },
                                                 "asset": {
                                                     "id": "testAssetByPresentation-multipleAssets-2",
                                                     "mediaType": "image/jpg",
                                                     "origin": "some/origin"
                                                 }
                                             }
                                         ] 
                                     }
                                     """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifestWithoutSpace);

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
    public async Task CreateManifest_ReturnsError_WhenErrorFromDlcs()
    {
        // Arrange
        var (slug, assetId) = TestIdentifiers.SlugResource();

        A.CallTo(() => DLCSApiClient.IngestAssets(Customer,
            A<List<JObject>>.That.Matches(o => o.First().GetValue("id").ToString() == assetId),
            A<CancellationToken>._)).Throws(new DlcsException("DLCS exception", HttpStatusCode.BadRequest));

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
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests",
                manifestWithoutSpace);
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();
        errorResponse!.Detail.Should().Be("DLCS exception");
    }

    [Fact]
    public async Task CreateManifest_MultipleImageComposition()
    {
        var (slug, _, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        var batchId = TestIdentifiers.BatchId();

        List<DBCanvasPainting> expected =
        [
            new()
            {
                Id = canvasId,
                CanvasOrder = 0,
                Label = new LanguageMap("en", "Background"),
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-2"),
                Ingesting = true,
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                Label = new LanguageMap("en", "Bottom Right"),
                Target = "xywh=800,800,100,50",
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-1"),
                Ingesting = true,
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 2,
                Label = new LanguageMap("en", "Top Left"),
                Target = "xywh=0,0,200,200",
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-0"),
                Ingesting = true,
            }
        ];

        // Manifest with 3 paintedResources. All share same canvasId as on same canvas but not a choice.
        // One is background image (no target), 2 target specific area of canvas
        var manifest = $$"""
                         {
                             "type": "Manifest",
                             "slug": "{{slug}}",
                             "parent": "http://localhost/{{Customer}}/collections/root",
                             "paintedResources": [
                                 {
                                     "canvasPainting": {
                                         "canvasId": "{{canvasId}}",
                                         "canvasOrder": 2,
                                         "target": "xywh=0,0,200,200",
                                         "label": {"en": ["Top Left"]}
                                     },
                                     "asset": {
                                         "id": "{{assetId}}-0",
                                         "batch": "{{batchId}}",
                                     }
                                 },
                                 {
                                     "canvasPainting": {
                                         "canvasId": "{{canvasId}}",
                                         "canvasOrder": 1,
                                         "target": "xywh=800,800,100,50",
                                         "label": {"en": ["Bottom Right"]}
                                     },
                                     "asset": {
                                         "id": "{{assetId}}-1"
                                     }
                                 },
                                 {
                                     "canvasPainting": {
                                         "canvasId": "{{canvasId}}",
                                         "canvasOrder": 0,
                                         "label": {"en": ["Background"]}
                                     },
                                     "asset": {
                                         "id": "{{assetId}}-2"
                                     }
                                 }
                             ]
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests",
                manifest);

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
    public async Task CreateManifest_MultipleImageComposition_NoCanvasOrderSpecified()
    {
        var (slug, _, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        var batchId = TestIdentifiers.BatchId();

        List<DBCanvasPainting> expected =
        [
            new()
            {
                Id = canvasId,
                CanvasOrder = 0,
                Label = new LanguageMap("en", "Top Left"),
                Target = "xywh=0,0,200,200",
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-0"),
                Ingesting = true,
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                Label = new LanguageMap("en", "Bottom Right"),
                Target = "xywh=800,800,100,50",
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-1"),
                Ingesting = true,
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 2,
                Label = new LanguageMap("en", "Background"),
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-2"),
                Ingesting = true,
            },
        ];

        // Manifest with 3 paintedResources. All share same canvasId as on same canvas but not a choice.
        // One is background image (no target), 2 target specific area of canvas
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
                                         "id": "{{assetId}}-0",
                                         "batch": "{{batchId}}",
                                     }
                                 },
                                 {
                                     "canvasPainting": {
                                         "canvasId": "{{canvasId}}",
                                         "target": "xywh=800,800,100,50",
                                         "label": {"en": ["Bottom Right"]}
                                     },
                                     "asset": {
                                         "id": "{{assetId}}-1"
                                     }
                                 },
                                 {
                                     "canvasPainting": {
                                         "canvasId": "{{canvasId}}",
                                         "label": {"en": ["Background"]}
                                     },
                                     "asset": {
                                         "id": "{{assetId}}-2"
                                     }
                                 }
                             ]
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests",
                manifest);

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
    public async Task CreateManifest_MultipleImageCompositionAndChoice()
    {
        var (slug, _, assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        var batchId = TestIdentifiers.BatchId();

        List<DBCanvasPainting> expected =
        [
            new()
            {
                Id = canvasId,
                CanvasOrder = 0,
                Label = new LanguageMap("en", "Background"),
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-0"),
                Ingesting = true,
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 1,
                Label = new LanguageMap("en", "Bottom Right"),
                Target = "xywh=800,800,100,50",
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-1"),
                Ingesting = true,
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 2,
                ChoiceOrder = 1,
                Label = new LanguageMap("en", "Choice 1"),
                Target = "xywh=0,0,200,200",
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-2"),
                Ingesting = true,
            },
            new()
            {
                Id = canvasId,
                CanvasOrder = 2,
                ChoiceOrder = 2,
                Label = new LanguageMap("en", "Choice 2"),
                Target = "xywh=0,0,200,200",
                CustomerId = Customer,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}-3"),
                Ingesting = true,
            }
        ];

        // Manifest with 4 paintedResources. All share same canvasId as on same canvas.
        // 2 are in a choice and 2 are not, so ultimately this would be 3 painting annos on Canvas
        var manifest = $$"""
                         {
                             "type": "Manifest",
                             "slug": "{{slug}}",
                             "parent": "http://localhost/{{Customer}}/collections/root",
                             "paintedResources": [
                                 {
                                     "canvasPainting": {
                                         "canvasId": "{{canvasId}}",
                                         "canvasOrder": 0,
                                         "label": {"en": ["Background"]}
                                     },
                                     "asset": {
                                         "id": "{{assetId}}-0",
                                         "batch": "{{batchId}}",
                                     }
                                 },
                                 {
                                     "canvasPainting": {
                                         "canvasId": "{{canvasId}}",
                                         "canvasOrder": 1,
                                         "target": "xywh=800,800,100,50",
                                         "label": {"en": ["Bottom Right"]}
                                     },
                                     "asset": {
                                         "id": "{{assetId}}-1"
                                     }
                                 },
                                 {
                                     "canvasPainting": {
                                         "canvasId": "{{canvasId}}",
                                         "canvasOrder": 2,
                                         "choiceOrder": 1,
                                         "target": "xywh=0,0,200,200",
                                         "label": {"en": ["Choice 1"]}
                                     },
                                     "asset": {
                                         "id": "{{assetId}}-2"
                                     }
                                 },
                                 {
                                     "canvasPainting": {
                                         "canvasId": "{{canvasId}}",
                                         "canvasOrder": 2,
                                         "choiceOrder": 2,
                                         "target": "xywh=0,0,200,200",
                                         "label": {"en": ["Choice 2"]}
                                     },
                                     "asset": {
                                         "id": "{{assetId}}-3"
                                     }
                                 }
                             ]
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests",
                manifest);

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
    public async Task CreateManifest_FindsAssetInDlcs_ThrowsExceptionWhenOnlyAsset()
    {
        // THIS TEST WILL FAIL ONCE #352 IS IMPLEMENTED

        // Arrange
        var slug = nameof(CreateManifest_FindsAssetInDlcs_ThrowsExceptionWhenOnlyAsset);
        var assetId = "testAssetByPresentation-exception-thrown";

        await dbContext.SaveChangesAsync();

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
                                                     "id": "fromDlcs_{{assetId}}_1",
                                                     "mediaType": "image/jpg"
                                                 }
                                             }
                                         ] 
                                     }
                                     """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests",
                manifestWithoutSpace);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
    
    [Fact]
    public async Task CreateManifest_FindsAssetInDlcs_WhenMixOfNewAndOldAssets()
    {
        // Arrange
        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer, 
                A<ICollection<string>>.That.Matches(o => o.First().Split('/', StringSplitOptions.None).Last().StartsWith("fromDlcs_")), 
                A<CancellationToken>._))
            .ReturnsLazily((int customerId, ICollection<string> assetIds, CancellationToken can) =>
                Task.FromResult((IList<JObject>)assetIds.Where(a => a.Split('/', StringSplitOptions.None).Last().StartsWith("fromDlcs_"))
                    .Select(x => JObject.Parse($$"""

                                                 {
                                                   "id": "{{x.Split('/').Last()}}",
                                                   "space": {{NewlyCreatedSpace}}
                                                 }
                                                 """)).ToList()));
        
        var slug = nameof(CreateManifest_FindsAssetInDlcs_WhenMixOfNewAndOldAssets);
        var assetId = "testAssetByPresentation-only-calls-new";
        
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
                                          "id": "fromDlcs_{{assetId}}_1",
                                          "mediaType": "image/jpg"
                                      }
                                  },
                                  {
                                     "canvasPainting":{
                                        "canvasOrder": 2
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
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests",
                manifestWithoutSpace);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(2);
        
        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());
        
        dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 1).Should().NotBeNull("asset added to manifest");
        
        A.CallTo(() => DLCSApiClient.UpdateAssetManifest(Customer, 
                A<List<string>>._, A<OperationType>._, A<List<string>>._, A<CancellationToken>._)).MustHaveHappened();
    }
}
