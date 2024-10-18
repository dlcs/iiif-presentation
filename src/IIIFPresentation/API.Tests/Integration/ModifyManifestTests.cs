using System.Net;
using System.Text;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Repository;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyManifestTests: IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private readonly IAmazonS3 amazonS3;
    private const int Customer = 1;

    public ModifyManifestTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));

        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task CreateManifest_Unauthorized_IfNoAuthTokenProvided()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", "{}");
        
        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateManifest_Forbidden_IfIncorrectShowExtraHeader()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{Customer}/manifests");
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "Incorrect");
        requestMessage.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    
    [Fact]
    public async Task CreateManifest_Forbidden_IfNoShowExtraHeader()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{Customer}/manifests");
        requestMessage.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}