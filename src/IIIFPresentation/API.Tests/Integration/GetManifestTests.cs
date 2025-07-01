#nullable disable

using System.Net;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using DLCS.API;
using FakeItEasy;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Models.API.Manifest;
using Models.Database.General;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using EntityTagHeaderValue = System.Net.Http.Headers.EntityTagHeaderValue;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class GetManifestTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;

    private readonly PresentationContext dbContext;

    private readonly IAmazonS3 amazonS3;
    private readonly IDlcsApiClient dlcsApiClient;
    private readonly IAmazonS3 s3;
    private readonly JObject sampleAsset;
    private const string PaintedResource = "foo-paintedResource";
    private const string IngestingPaintedResource = "ingestingPaintedResource";

    public GetManifestTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        s3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        dbContext = storageFixture.DbFixture.DbContext;
        dlcsApiClient = A.Fake<IDlcsApiClient>();
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services => services.AddSingleton(dlcsApiClient));

        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

        storageFixture.DbFixture.CleanUp();

        sampleAsset = JObject.Parse(
            """
            {
                   "@context": "https://localhost/contexts/Image.jsonld",
                   "@id": "https://localhost:7230/customers/1/spaces/2/images/foo-paintedResource",
                   "@type": "vocab:Image",
                   "id": "foo-paintedResource",
                   "ingesting": false,
                   "space": 1
                 }
            """
        );
        
        var errorAsset = JObject.Parse(
            """
            {
                   "@context": "https://localhost/contexts/Image.jsonld",
                   "@id": "https://localhost:7230/customers/1/spaces/2/images/errorPaintedResource",
                   "@type": "vocab:Image",
                   "id": "errorPaintedResource",
                   "error": "random error",
                   "space": 1
                 }
            """
        );
        
        var ingestingAsset = JObject.Parse(
            """
            {
                   "@context": "https://localhost/contexts/Image.jsonld",
                   "@id": "https://localhost:7230/customers/1/spaces/2/images/foo-paintedResource",
                   "@type": "vocab:Image",
                   "id": "ingestingPaintedResource",
                   "ingesting": true,
                   "space": 1
                 }
            """
        );
        
        A.CallTo(() => dlcsApiClient.GetCustomerImages(PresentationContextFixture.CustomerId,
                A<IList<string>>.That.Matches(l =>
                    l.Any(x => $"1/2/{PaintedResource}".Equals(x))),
                A<CancellationToken>._))
            .ReturnsLazily(() => [sampleAsset]);
        
        A.CallTo(() => dlcsApiClient.GetCustomerImages(PresentationContextFixture.CustomerId,
                A<IList<string>>.That.Matches(l =>
                    l.Any(x => $"1/2/{IngestingPaintedResource}".Equals(x))),
                A<CancellationToken>._))
            .ReturnsLazily(() => [ingestingAsset, errorAsset]);
    }
    
    [Fact]
    public async Task Get_Hierarchical_ReturnsNotFound_WhenAuthAndShowExtrasHeaders_IfNotFound()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/no-here");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_Hierarchical_ReturnsNotFound_WhenNoAuth_IfNotFound()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/no-here");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_Hierarchical_ReturnsSeeOther_WhenAuthAndShowExtrasHeaders()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/iiif-manifest");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.Should().Be("http://localhost/1/manifests/FirstChildManifest");
    }
    
    [Fact]
    public async Task Get_Flat_ReturnsNotFound_WhenNotExtraHeaders()
    {
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/manifests/no-here");
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_IiifManifest_Flat_ReturnsRedirect_WhenNoExtraHeaders()
    {
        // Arrange and Act
        var requestMessage =
            new HttpRequestMessage(HttpMethod.Get, "1/manifests/FirstChildManifest");
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.AbsolutePath.Should().Be("/1/iiif-manifest");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsCorrectRedirect_WhenSecondCustomer()
    {
        // Arrange
        await dbContext.Manifests.AddTestManifest(customer: 10, slug: "iiif-manifest", id: "FirstChildManifest");
        await dbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.GetAsync("10/manifests/FirstChildManifest");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.AbsolutePath.Should().Be("/10/iiif-manifest");
    }

    [Fact]
    public async Task Get_IiifManifest_Flat_ReturnsManifestFromS3_DecoratedWithDbValues()
    {
        // Arrange and Act
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/manifests/FirstChildManifest");
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var manifest = await response.ReadAsPresentationJsonAsync<PresentationManifest>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey(HeaderNames.ETag);
        response.Headers.Vary.Should().HaveCount(2);
        manifest.Should().NotBeNull();
        manifest!.Type.Should().Be("Manifest");
        manifest.Id.Should().Be("http://localhost/1/manifests/FirstChildManifest", "requested by flat URI");
        manifest.Items.Should().HaveCount(3, "the test content contains 3 children");
        manifest.FlatId.Should().Be("FirstChildManifest");
        manifest.PublicId.Should().Be("http://localhost/1/iiif-manifest", "iiif-manifest is slug and under root");
    }

    [Fact]
    public async Task Get_IiifManifest_Flat_ReturnsManifestFromFinalS3_IfStagingMissing()
    {
        // Arrange and Act
        const string id = "AStillIngestingManifest";
        var e = await dbContext.Manifests.AddAsync(new()
        {
            Id = "AStillIngestingManifest",
            CustomerId = 1,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = "an-iiif-manifest-ingesting",
                    Parent = RootCollection.Id,
                    Type = ResourceType.IIIFManifest,
                    Canonical = true
                }
            ],
            Batches =
            [
                new()
                {
                    Id = -17,
                    CustomerId = 1,
                    ManifestId = "AStillIngestingManifest",
                    Status = BatchStatus.Ingesting
                }
            ],
            LastProcessed = DateTime.UtcNow
        });
       
            await dbContext.SaveChangesAsync();
            var requestMessage =
                HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/manifests/{id}");
            var response = await httpClient.AsCustomer().SendAsync(requestMessage);

            var manifest = await response.ReadAsPresentationJsonAsync<PresentationManifest>();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            response.Headers.Should().NotContainKey(HeaderNames.ETag);
            response.Headers.Vary.Should().HaveCount(2);
            manifest.Should().NotBeNull();
            manifest!.Type.Should().Be("Manifest");
            manifest.Id.Should().Be($"http://localhost/1/manifests/{id}", "requested by flat URI");
            manifest.Items.Should().HaveCount(3, "the test content contains 3 children");
            manifest.FlatId.Should().Be("AStillIngestingManifest");
            manifest.PublicId.Should().Be("http://localhost/1/an-iiif-manifest-ingesting",
                "an-iiif-manifest-ingesting is slug and under root");
       
    }

    [Fact]
    public async Task Get_IiifManifest_Flat_ReturnsManifestFromS3_DecoratedWithPaintedResources()
    {
        // Arrange - add manifest with 1 canvasPainting with an asset and corresponding manifest in S3
        var id = nameof(Get_IiifManifest_Flat_ReturnsManifestFromS3_DecoratedWithPaintedResources);
        var dbManifest = await dbContext.Manifests.AddTestManifest(id);
        var assetId = new AssetId(1, 2, PaintedResource);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest.Entity, label: new LanguageMap("en", "foo"),
            assetId: assetId);
        await dbContext.SaveChangesAsync();
        await s3.PutObjectAsync(new()
        {
            BucketName = LocalStackFixture.StorageBucketName,
            Key = $"1/manifests/{id}",
            ContentBody = TestContent.ManifestJson,
        });

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/manifests/{id}");
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Act
        var manifest = await response.ReadAsPresentationJsonAsync<PresentationManifest>();

        // Assert
        manifest.Should().NotBeNull();
        manifest.PaintedResources.Should().HaveCount(1);
        var paintedResource = manifest.PaintedResources.Single();
        paintedResource.CanvasPainting.Label.Should().BeEquivalentTo(new LanguageMap("en", "foo"));
        paintedResource.Asset.Should().BeEquivalentTo(sampleAsset);
        manifest.Ingesting.Should().BeNull();
    }

    [Fact]
    public async Task Get_IiifManifest_Hierarchical_ReturnsManifestFromS3()
    {
        // Arrange and Act
        var response = await httpClient.GetAsync("1/iiif-manifest");

        var manifest = await response.ReadAsPresentationJsonAsync<Manifest>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey(HeaderNames.ETag);
        response.Headers.Vary.Should().HaveCount(2);
        manifest.Should().NotBeNull();
        manifest!.Type.Should().Be("Manifest");
        manifest.Id.Should().Be("http://localhost/1/iiif-manifest", "requested by hierarchical URI");
        manifest.Items.Should().HaveCount(3, "the test content contains 3 children");
    }
    
    [Fact]
    public async Task Get_IiifManifest_Hierarchical_Returns304ForPreviouslyReturnedETag()
    {
        // First call (no etag)
        // Arrange and Act
        var response = await httpClient.GetAsync("1/iiif-manifest");

        var manifest = await response.ReadAsPresentationJsonAsync<Manifest>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey(HeaderNames.ETag);
        manifest.Should().NotBeNull();
        manifest!.Type.Should().Be("Manifest");
        manifest.Id.Should().Be("http://localhost/1/iiif-manifest", "requested by hierarchical URI");
        manifest.Items.Should().HaveCount(3, "the test content contains 3 children");
        
        // Second call (with etag)
        // Arrange and Act
        var request = new HttpRequestMessage(HttpMethod.Get, "1/iiif-manifest");
        request.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotModified);
        response.Headers.Should().ContainKey(HeaderNames.ETag);
    }

    [Fact]
    public async Task Get_IiifManifest_Flat_ReturnsAccepted_WhenIngesting()
    {
        var id = nameof(Get_IiifManifest_Flat_ReturnsAccepted_WhenIngesting);

        // Arrange and Act
        var dbManifest = await dbContext.Manifests.AddTestManifest(id, batchId: 1);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest.Entity,
            createdDate: DateTime.UtcNow.AddDays(-1),
            assetId: new AssetId(1, 2, PaintedResource),
            height: 1800, width: 1200,
            canvasOriginalId: new Uri("https://iiif.io/api/eclipse"));
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest.Entity,
            createdDate: DateTime.UtcNow.AddDays(-1),
            assetId: new AssetId(1, 2, IngestingPaintedResource),
            height: 1800, width: 1200,
            canvasOriginalId: new Uri("https://iiif.io/api/eclipse"));

        await amazonS3.PutObjectAsync(new()
        {
            BucketName = LocalStackFixture.StorageBucketName,
            Key = $"1/manifests/{id}",
            ContentBody = TestContent.ManifestJson
        });

        await dbContext.SaveChangesAsync();

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/manifests/{id}");
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var manifest = await response.ReadAsPresentationJsonAsync<PresentationManifest>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.Should().NotContainKey(HeaderNames.ETag, "should not be modified when ingesting");
        response.Headers.Vary.Should().HaveCount(2);
        manifest.Should().NotBeNull();
        manifest!.Type.Should().Be("Manifest");
        manifest.Id.Should().Be($"http://localhost/1/manifests/{id}", "requested by flat URI");
        manifest.Items.Should().HaveCount(3, "the test content contains 3 children");
        manifest.FlatId.Should().Be(id);
        manifest.PublicId.Should().Be($"http://localhost/1/sm_{id}", "iiif-manifest is slug and under root");
        manifest.Ingesting.Should().BeEquivalentTo(new IngestingAssets
        {
            Total = 2,
            Finished = 0,
            Errors = 1
        });
    }

    [Fact]
    public async Task Get_IiifManifest_Hierarchical_ReturnsNotFoundWhenIngesting()
    {
        // Arrange
        var id = nameof(Get_IiifManifest_Hierarchical_ReturnsNotFoundWhenIngesting);
        await dbContext.Manifests.AddTestManifest(id, batchId: 2);

        await amazonS3.PutObjectAsync(new()
        {
            BucketName = LocalStackFixture.StorageBucketName,
            Key = $"1/manifests/{id}",
            ContentBody = TestContent.ManifestJson
        });

        await dbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"1/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Vary.Should().HaveCount(2);
    }

    [Fact]
    public async Task Get_IiifManifest_Hierarchical_ReturnsOkWhenIngestingButHasIngestedBefore()
    {
        // Arrange
        var id = nameof(Get_IiifManifest_Hierarchical_ReturnsOkWhenIngestingButHasIngestedBefore);

        await dbContext.Manifests.AddTestManifest(id, batchId: 3, ingested: true);

        await amazonS3.PutObjectAsync(new()
        {
            BucketName = LocalStackFixture.StorageBucketName,
            Key = $"1/manifests/{id}",
            ContentBody = TestContent.ManifestJson
        });

        await dbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"1/sm_{id}");

        var manifest = await response.ReadAsPresentationJsonAsync<PresentationManifest>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey(HeaderNames.ETag);
        response.Headers.Vary.Should().HaveCount(2);
        manifest.Should().NotBeNull();
        manifest!.Type.Should().Be("Manifest");
        manifest.Id.Should().Be($"http://localhost/1/sm_{id}", "requested by hierarchical URI");
        manifest.Items.Should().HaveCount(3, "the test content contains 3 children");
    }
}
