using System.Net;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class TrailingSlashRedirectTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;

    public TrailingSlashRedirectTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));
        storageFixture.DbFixture.CleanUp();
    }
    
    [Theory]
    [InlineData("1/collections/root/", HttpStatusCode.Found, "/1/collections/root")]
    [InlineData("1/manifests/root/", HttpStatusCode.Found, "/1/manifests/root")]
    [InlineData("1/canvases/root/", HttpStatusCode.Found, "/1/canvases/root")]
    [InlineData("1/hierarchical/path/", HttpStatusCode.Found, "/1/hierarchical/path")]
    [InlineData("some/random/path/", HttpStatusCode.Found, "/some/random/path")]
    [InlineData("1/", HttpStatusCode.Found, "/1")]
    [InlineData("1/test/", HttpStatusCode.Found, "/1/test")]
    [InlineData("/", HttpStatusCode.Found, "")]
    public async Task TrailingSlash_StandardPathMatches(string path, HttpStatusCode expectedStatusCode, string expectedPath)
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, path);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(expectedStatusCode);
        response.Headers.Location!.Should().Be($"http://localhost{expectedPath}");
    }
    
    [Theory]
    [InlineData("1/collections/root/", HttpStatusCode.Found, "http://example.com/foo/1/collections/root")]
    [InlineData("1/manifests/root/", HttpStatusCode.Found, "http://example.com/example/1/manifests/root")]
    [InlineData("1/canvases/root/", HttpStatusCode.Found, "http://example.com/example/1/canvases/root")]
    [InlineData("1/hierarchical/path/", HttpStatusCode.Found, "http://example.com/example/1/hierarchical/path")]
    [InlineData("some/random/path/", HttpStatusCode.Found, "http://example.com/some/random/path")]
    [InlineData("1/", HttpStatusCode.Found, "http://example.com/example/1")]
    [InlineData("1/test/", HttpStatusCode.Found, "http://example.com/example/1/test")]
    [InlineData("/", HttpStatusCode.Found, "http://example.com")]
    public async Task TrailingSlash_RewrittenPathMatches(string path, HttpStatusCode expectedStatusCode, string expectedPath)
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, path);
        HttpRequestMessageBuilder.AddHostExampleHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(expectedStatusCode);
        response.Headers.Location!.Should().Be(expectedPath);
    }
    
    [Fact]
    public async Task TrailingSlash_NoRedirect_WhenNotGet()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "some/path/");
        HttpRequestMessageBuilder.AddHostExampleHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
