using System.Net;
using API.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class LegacyHostRedirectMiddlewareTests : IClassFixture<PresentationAppFactory<Program>>
{
    private const string LegacyHost = "legacy.example.com";

    private readonly HttpClient httpClient;

    public LegacyHostRedirectMiddlewareTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture)
                .WithConfigValue("PathSettings:LegacyPresentationApiUrl", $"https://{LegacyHost}"));
        storageFixture.DbFixture.CleanUp();
    }

    private static void AddLegacyHostHeader(HttpRequestMessage requestMessage) =>
        requestMessage.Headers.Add("Host", LegacyHost);

    [Fact]
    public async Task Get_HierarchicalPath_RedirectsToDefaultHost()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/some/hierarchical/path");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/some/hierarchical/path");
    }

    [Fact]
    public async Task Get_HierarchicalPath_WithQueryString_PreservesQueryString()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/some/hierarchical/path?foo=bar");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/some/hierarchical/path?foo=bar");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("POST")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task MutatingMethods_RedirectWithPermanentRedirect(string method)
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(new HttpMethod(method), "1/manifests/FirstChildManifest");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/manifests/FirstChildManifest");
    }

    [Fact]
    public async Task Get_FlatManifest_Anonymous_RedirectsToSameFlatPath_OnDefaultHost()
    {
        // Arrange - a plain host swap, even though this itself would 303 to the hierarchical path once it lands
        // on the new host; decided against combining the two hops into one (dlcs/iiif-presentation#653)
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/manifests/FirstChildManifest");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/manifests/FirstChildManifest");
    }

    [Fact]
    public async Task Get_HierarchicalManifest_Authorised_RedirectsToSameHierarchicalPath_OnDefaultHost()
    {
        // Arrange - a plain host swap, even though this itself would 303 to the flat path once it lands on the
        // new host; decided against combining the two hops into one (dlcs/iiif-presentation#653)
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/iiif-manifest");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/iiif-manifest");
    }

    [Fact]
    public async Task Get_NonExistentPath_StillRedirectsToSamePath_OnDefaultHost()
    {
        // Arrange - the middleware doesn't resolve the resource at all, so a not-found path redirects just the same
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/manifests/no-here");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/manifests/no-here");
    }

    [Fact]
    public async Task Get_NonLegacyHost_IsNotRedirected()
    {
        // Act - no Host header override, so this hits the default test host rather than the configured legacy one
        var response = await httpClient.GetAsync("1/manifests/no-here");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
