#nullable disable

using System.Net;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using IIIF.Presentation.V3;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class GetManifestTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private const int TotalDatabaseChildItems = 4;

    public GetManifestTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));

        storageFixture.DbFixture.CleanUp();
    }


    [Fact]
    public async Task Get_IiifManifest_Flat_ReturnsManifestFromS3()
    {
        // Arrange and Act
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/manifests/FirstChildManifest");
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var manifest = await response.ReadAsPresentationJsonAsync<Manifest>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        manifest.Should().NotBeNull();
        manifest!.Type.Should().Be("Manifest");
        manifest.Id.Should().Be("http://localhost/1/manifests/FirstChildManifest", "requested by flat URI");
        manifest.Items.Should().HaveCount(3, "the test content contains 3 children");
    }

    [Fact]
    public async Task Get_IiifManifest_Hierarchical_ReturnsManifestFromS3()
    {
        // Arrange and Act
        var response = await httpClient.GetAsync("1/iiif-manifest");

        var manifest = await response.ReadAsPresentationJsonAsync<Manifest>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        manifest.Should().NotBeNull();
        manifest!.Type.Should().Be("Manifest");
        manifest.Id.Should().Be("http://localhost/1/iiif-manifest", "requested by hierarchical URI");
        manifest.Items.Should().HaveCount(3, "the test content contains 3 children");
    }
}