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
    [InlineData("1/collections/root/", HttpStatusCode.Found, "http://localhost/1/collections/root")]
    [InlineData("1/manifests/root/", HttpStatusCode.Found, "http://localhost/1/manifests/root")]
    [InlineData("1/canvases/root/", HttpStatusCode.Found, "http://localhost/1/canvases/root")]
    [InlineData("1/hierarchical/path/", HttpStatusCode.Found, "http://localhost/1/hierarchical/path")]
    [InlineData("some/random/path/", HttpStatusCode.Found, "http://localhost/some/random/path")]
    [InlineData("1/", HttpStatusCode.Found, "http://localhost/1")]
    [InlineData("1/test/", HttpStatusCode.Found, "http://localhost/1/test")]
    [InlineData("/", HttpStatusCode.NotFound, null)]
    public async Task TrailingSlash_StandardPathMatches(string path, HttpStatusCode expectedStatusCode, string? expectedPath)
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, path);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(expectedStatusCode);
        response.Headers.Location!.Should().Be(expectedPath);
    }
    
    // 401 is a customer that doesn't exist in the test system - will cause NotFound to be returned if no redirect
    [Theory]
    [InlineData("401/collections/root/", HttpStatusCode.Found, "http://example.com/foo/401/collections/root")]
    [InlineData("401/manifests/root/", HttpStatusCode.Found, "http://example.com/example/401/manifests/root")]
    [InlineData("401/canvases/root/", HttpStatusCode.Found, "http://example.com/example/401/canvases/root")]
    [InlineData("401/hierarchical/path/", HttpStatusCode.Found, "http://example.com/example/401/hierarchical/path")]
    [InlineData("some/random/path/", HttpStatusCode.Found, "http://example.com/some/random/path")]
    [InlineData("401/", HttpStatusCode.Found, "http://example.com/example/401")]
    [InlineData("401/test/", HttpStatusCode.Found, "http://example.com/example/401/test")]
    [InlineData("/", HttpStatusCode.NotFound, null)]
    public async Task TrailingSlash_RewrittenPathMatches(string path, HttpStatusCode expectedStatusCode, string? expectedPath)
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, path);
        HttpRequestMessageBuilder.AddHostExampleHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer(401).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(expectedStatusCode);
        response.Headers.Location?.Should().Be(expectedPath);
    }
    
    // 402 is a customer that doesn't exist in the test system - will cause NotFound to be returned if no redirect
    [Theory]
    [InlineData("402/collections/root/", HttpStatusCode.Found, "http://no-customer.com/collections/root")]
    [InlineData("402/manifests/root/", HttpStatusCode.Found, "http://no-customer.com/manifests/root")]
    [InlineData("402/canvases/root/", HttpStatusCode.Found, "http://no-customer.com/canvases/root")]
    [InlineData("402/hierarchical/path/", HttpStatusCode.Found, "http://no-customer.com/hierarchical/path")]
    [InlineData("some/random/path/", HttpStatusCode.Found, "http://no-customer.com/some/random/path")] // this can't happen in the wild (as it means they passed through without an origin adding a customer), but useful to test
    [InlineData("402/test/", HttpStatusCode.Found, "http://no-customer.com/test")]
    [InlineData("/", HttpStatusCode.NotFound, null)] // this can't happen in the wild (as it means they passed through without an origin adding a customer), but useful to test
    [InlineData("402/", HttpStatusCode.NotFound, null)] // host redirect detected, so allowed through. i.e.: found "http://no-customer.com"
    public async Task TrailingSlash_RewrittenPathMatches_NoCustomerRedirects(string path, HttpStatusCode expectedStatusCode, string? expectedPath)
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, path);
        HttpRequestMessageBuilder.AddHostNoCustomerHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer(402).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(expectedStatusCode);
        response.Headers.Location?.Should().Be(expectedPath);
    }
    
    // 403 is a customer that doesn't exist in the test system - will cause NotFound to be returned if no redirect
    [Theory]
    [InlineData("403/collections/root/", HttpStatusCode.Found, "http://no-customer-additional-path-element.com/test/collections/root")]
    [InlineData("403/manifests/root/", HttpStatusCode.Found, "http://no-customer-additional-path-element.com/test/manifests/root")]
    [InlineData("403/canvases/root/", HttpStatusCode.Found, "http://no-customer-additional-path-element.com/test/canvases/root")]
    [InlineData("403/hierarchical/path/", HttpStatusCode.Found, "http://no-customer-additional-path-element.com/test/hierarchical/path")]
    [InlineData("some/random/path/", HttpStatusCode.Found, "http://no-customer-additional-path-element.com/some/random/path")] // this can't happen in the wild (as it means they passed through without an origin adding a customer), but useful to test
    [InlineData("403/test/", HttpStatusCode.Found, "http://no-customer-additional-path-element.com/test/test")]
    [InlineData("/", HttpStatusCode.NotFound, null)] // this can't happen in the wild (as it means they passed through without an origin adding a customer), but useful to test
    [InlineData("403/", HttpStatusCode.NotFound, null)] // host redirect detected for a forwarded subdirectory, so allowed through. i.e.: found "http://no-customer-additional-path-element.com/test"
    public async Task TrailingSlash_RewrittenPathMatches_NoCustomerWithAdditionalPathElementRedirects(string path, HttpStatusCode expectedStatusCode, string? expectedPath)
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, path);
        HttpRequestMessageBuilder.AddHostNoCustomerAdditionalPathElementHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer(402).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(expectedStatusCode);
        response.Headers.Location?.Should().Be(expectedPath);
    }
    
    // 404 is a customer that doesn't exist in the test system - will cause NotFound to be returned if no redirect
    [Theory]
    [InlineData("404/collections/root/", HttpStatusCode.Found, "http://no-customer-multiple-path-element.com/has/multiple/additional/elements/collections/root")]
    [InlineData("404/manifests/root/", HttpStatusCode.Found, "http://no-customer-multiple-path-element.com/has/multiple/additional/elements/manifests/root")]
    [InlineData("404/canvases/root/", HttpStatusCode.Found, "http://no-customer-multiple-path-element.com/has/multiple/additional/elements/canvases/root")]
    [InlineData("404/hierarchical/path/", HttpStatusCode.Found, "http://no-customer-multiple-path-element.com/has/multiple/additional/elements/hierarchical/path")]
    [InlineData("some/random/path/", HttpStatusCode.Found, "http://no-customer-multiple-path-element.com/some/random/path")] // this can't happen in the wild (as it means they passed through without an origin adding a customer), but useful to test
    [InlineData("404/test/", HttpStatusCode.Found, "http://no-customer-multiple-path-element.com/has/multiple/additional/elements/test")]
    [InlineData("/", HttpStatusCode.NotFound, null)] // this can't happen in the wild (as it means they passed through without an origin adding a customer), but useful to test
    [InlineData("404/", HttpStatusCode.NotFound, null)] // host redirect detected for a forwarded subdirectory, so allowed through. i.e.: found "http://no-customer-multiple-path-element.com/has/multiple/additional/elements"
    public async Task TrailingSlash_RewrittenPathMatches_NoCustomerWithMultiplePathElementRedirects(string path, HttpStatusCode expectedStatusCode, string? expectedPath)
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, path);
        HttpRequestMessageBuilder.AddHostNoCustomerMultiplePathElementHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer(402).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(expectedStatusCode);
        response.Headers.Location?.Should().Be(expectedPath);
    }
    
    // 405 is a customer that doesn't exist in the test system - will cause NotFound to be returned if no redirect
    [Theory]
    [InlineData("405/collections/root/", HttpStatusCode.Found, "https://fully_qualified.co.uk/405/collections/root")]
    [InlineData("405/manifests/root/", HttpStatusCode.Found, "https://fully_qualified.co.uk/405/manifests/root")]
    [InlineData("405/canvases/root/", HttpStatusCode.Found, "https://fully_qualified.co.uk/405/canvases/root")]
    [InlineData("405/hierarchical/path/", HttpStatusCode.Found, "https://fully_qualified.co.uk/405/hierarchical/path")]
    [InlineData("some/random/path/", HttpStatusCode.Found, "http://fully-qualified.com/some/random/path")] // cannot pick up a customer, so just return with calling path
    [InlineData("405/test/", HttpStatusCode.Found, "https://fully_qualified.co.uk/405/test")]
    [InlineData("/", HttpStatusCode.NotFound, null)] 
    [InlineData("405/", HttpStatusCode.Found, "https://fully_qualified.co.uk/405")]
    public async Task TrailingSlash_RewrittenPathMatches_FullyQualifiedCustomerRedirects(string path, HttpStatusCode expectedStatusCode, string? expectedPath)
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, path);
        HttpRequestMessageBuilder.AddHostFullyQualifiedCustomerHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer(402).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(expectedStatusCode);
        response.Headers.Location?.Should().Be(expectedPath);
    }
    
    [Theory]
    [InlineData("406/collections/root/", HttpStatusCode.Found, "https://fully_qualified_no_customer.co.uk/collections/root")]
    [InlineData("406/manifests/root/", HttpStatusCode.Found, "https://fully_qualified_no_customer.co.uk/manifests/root")]
    [InlineData("406/canvases/root/", HttpStatusCode.Found, "https://fully_qualified_no_customer.co.uk/canvases/root")]
    [InlineData("406/hierarchical/path/", HttpStatusCode.Found, "https://fully_qualified_no_customer.co.uk/hierarchical/path")]
    [InlineData("some/random/path/", HttpStatusCode.Found, "http://fully-qualified-no-customer.com/some/random/path")] // this can't happen in the wild (as it means they passed through without an origin adding a customer), but useful to test
    [InlineData("406/test/", HttpStatusCode.Found, "https://fully_qualified_no_customer.co.uk/test")]
    [InlineData("/", HttpStatusCode.NotFound, null)] // this can't happen in the wild (as it means they passed through without an origin adding a customer), but useful to test
    [InlineData("406/", HttpStatusCode.NotFound, null)] // host redirect detected for a forwarded subdirectory, so allowed through. i.e.: found "https://fully_qualified_no_customer.co.uk"
    public async Task TrailingSlash_RewrittenPathMatches_FullyQualifiedNoCustomerRedirects(string path, HttpStatusCode expectedStatusCode, string? expectedPath)
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, path);
        HttpRequestMessageBuilder.AddHostFullyQualifiedNoCustomerHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer(402).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(expectedStatusCode);
        response.Headers.Location?.Should().Be(expectedPath);
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
