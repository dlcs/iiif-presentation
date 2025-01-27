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
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Test.Helpers.Helpers;
using Models.Database.General;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class GetManifestTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    
    private readonly PresentationContext dbContext;
    
    private readonly IAmazonS3 amazonS3;
    private readonly PresentationContext dbContext;
    private readonly IDlcsApiClient dlcsApiClient;
    private readonly IAmazonS3 s3;
    private readonly JObject sampleAsset;

    public GetManifestTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        s3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        dbContext = storageFixture.DbFixture.DbContext;
        dlcsApiClient = A.Fake<IDlcsApiClient>();
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services => services.AddSingleton(dlcsApiClient));
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));

        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

        storageFixture.DbFixture.CleanUp();

        sampleAsset = JObject.Parse(
            """
            {
                   "@context": "https://localhost/contexts/Image.jsonld",
                   "@id": "https://localhost:7230/customers/1/spaces/2/images/foo-paintedResource",
                   "@type": "vocab:Image",
                   "id": "foo-paintedResource",
                   "space": 1
                 }
            """
        );
        A.CallTo(() => dlcsApiClient.GetCustomerImages(PresentationContextFixture.CustomerId,
                A<IList<string>>.That.Matches(l =>
                    l.Any(x => "1/2/foo-paintedResource".Equals(x))),
                A<CancellationToken>._))
            .ReturnsLazily(() => [sampleAsset]);
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
    public async Task Get_IiifManifest_Flat_ReturnsManifestFromS3_DecoratedWithPaintedResources()
    {
        // Arrange - add manifest with 1 canvasPainting with an asset and corresponding manifest in S3
        var id = nameof(Get_IiifManifest_Flat_ReturnsManifestFromS3_DecoratedWithPaintedResources);
        var dbManifest = await dbContext.Manifests.AddTestManifest(id);
        var assetId = new AssetId(1, 2, "foo-paintedResource");
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
    }

    [Fact]
    public async Task Get_IiifManifest_Hierarchical_Returns_TrailingSlashRedirect()
    {
        // Arrange and Act
        var response = await httpClient.GetAsync("1/iiif-manifest/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.Should().Be("/1/iiif-manifest");
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
    public async Task Get_IiifManifest_Flat_ReturnsAccepted_WhenIngesting()
    {
        // Arrange and Act
        await dbContext.Manifests.AddAsync(new Models.Database.Collections.Manifest()
        {
            Id = "ManifestWithBatch",
            CustomerId = 1,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = "manifest-with-batch",
                    Parent = RootCollection.Id,
                    Type = ResourceType.IIIFManifest,
                    Canonical = true
                }
            ],
            Batches =
            [
                new Batch
                {
                    Id = 1,
                    Submitted = DateTime.UtcNow,
                    Status = BatchStatus.Ingesting,
                    ManifestId = "ManifestWithBatch"
                }
            ]
        });
        
        await amazonS3.PutObjectAsync(new()
        {
            BucketName = LocalStackFixture.StorageBucketName,
            Key = "1/manifests/ManifestWithBatch",
            ContentBody = TestContent.ManifestJson
        });
        
        await dbContext.SaveChangesAsync();
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/manifests/ManifestWithBatch");
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var manifest = await response.ReadAsPresentationJsonAsync<PresentationManifest>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.Should().ContainKey(HeaderNames.ETag);
        response.Headers.Vary.Should().HaveCount(2);
        manifest.Should().NotBeNull();
        manifest!.Type.Should().Be("Manifest");
        manifest.Id.Should().Be("http://localhost/1/manifests/ManifestWithBatch", "requested by flat URI");
        manifest.Items.Should().HaveCount(3, "the test content contains 3 children");
        manifest.FlatId.Should().Be("ManifestWithBatch");
        manifest.PublicId.Should().Be("http://localhost/1/manifest-with-batch", "iiif-manifest is slug and under root");
    }
}
