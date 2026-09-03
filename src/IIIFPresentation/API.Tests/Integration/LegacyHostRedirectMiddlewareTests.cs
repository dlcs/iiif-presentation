using System.Net;
using API.Tests.Integration.Infrastructure;
using Test.Helpers.Helpers;
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
        // Arrange - not a flat manifest/collection path, so this is a plain host swap rather than a combined redirect
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
    public async Task Get_FlatManifest_Anonymous_RedirectsStraightToHierarchicalPath_OnDefaultHost()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/manifests/FirstChildManifest");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/iiif-manifest",
            "combines the legacy host swap and the flat -> hierarchical redirect into a single hop");
    }

    [Fact]
    public async Task Get_FlatManifest_Authorised_RedirectsToFlatPath_OnDefaultHost()
    {
        // Arrange - ShowExtras header + valid auth header means this would return full content on the new host,
        // so it isn't safe to jump straight to the public hierarchical view
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/manifests/FirstChildManifest");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/manifests/FirstChildManifest",
            "authorised requests are redirected so they can re-authorise against the flat url on the new host");
    }

    [Fact]
    public async Task Get_FlatManifest_NotFound_FallsBackToPlainHostSwap()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/manifests/no-here");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/manifests/no-here");
    }

    [Fact]
    public async Task Get_FlatCollection_Anonymous_RedirectsStraightToHierarchicalPath_OnDefaultHost()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"1/collections/{RootCollection.Id}");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1");
    }

    [Fact]
    public async Task Get_HierarchicalManifest_Authorised_RedirectsStraightToFlatPath_OnDefaultHost()
    {
        // Arrange - the symmetric case to Get_FlatManifest_Anonymous_RedirectsStraightToHierarchicalPath_OnDefaultHost:
        // an authorised request to the hierarchical path would itself redirect to the flat path on the new host, so
        // this combines both hops into one
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/iiif-manifest");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/manifests/FirstChildManifest",
            "combines the legacy host swap and the hierarchical -> flat redirect into a single hop");
    }

    [Fact]
    public async Task Get_HierarchicalCollection_Authorised_RedirectsStraightToFlatPath_OnDefaultHost()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/iiif-collection");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/collections/IiifCollection",
            "combines the legacy host swap and the hierarchical -> flat redirect into a single hop");
    }

    [Fact]
    public async Task Get_HierarchicalCollection_Authorised_WithQueryString_PreservesQueryString_OnFlatRedirect()
    {
        // Arrange - the root collection is a storage collection, so (matching StorageController.GetHierarchical)
        // its pagination query params should carry over to the combined flat-url redirect; manifests have no such
        // params, so they're deliberately not covered by this
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1?page=2&pageSize=2");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be(
            $"https://localhost:7230/1/collections/{RootCollection.Id}?page=2&pageSize=2");
    }

    [Fact]
    public async Task Get_FlatManifest_Anonymous_DifferentCasing_StillRedirectsStraightToHierarchicalPath()
    {
        // Arrange - "Manifests" rather than "manifests": still matches the flat manifest route once actually
        // routed on the target host (ASP.NET Core route matching is case-insensitive), so the combined redirect
        // needs to recognise it too, rather than misreading it as a hierarchical slug
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/Manifests/FirstChildManifest");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/iiif-manifest");
    }

    [Fact]
    public async Task Get_HierarchicalPath_Anonymous_FallsBackToPlainHostSwap()
    {
        // Arrange - anonymous requests to a hierarchical path already get full content directly on the new host,
        // so there's no further redirect to combine into this one
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/iiif-manifest");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/iiif-manifest");
    }

    [Fact]
    public async Task Get_HierarchicalPath_Authorised_NotFound_FallsBackToPlainHostSwap()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/not-here");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/not-here");
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
