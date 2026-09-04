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
    public async Task MutatingMethods_Anonymous_RedirectWithPermanentRedirect(string method)
    {
        // Arrange - no Authorization header, so there's nothing at risk of being dropped on the client's redirect
        // follow
        var requestMessage = new HttpRequestMessage(new HttpMethod(method), "1/manifests/FirstChildManifest");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/manifests/FirstChildManifest");
    }

    [Fact]
    public async Task Delete_Authorised_ProcessesInPlace_WithDeprecationHeaders_InsteadOfRedirect()
    {
        // Arrange - carries an Authorization header, so this must not be redirected even though it's a mutating
        // method: browsers/HttpClient/curl all strip Authorization when auto-following a redirect to a different
        // host, which would silently drop the caller's credentials. Processed in place instead, with the legacy
        // host flagged as deprecated via response headers
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete, "1/manifests/no-here");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert - never redirected (no Location header set by this middleware), regardless of what the
        // downstream handler made of the request
        response.Headers.Location.Should().BeNull();
        response.Headers.GetValues("Deprecation").Should().ContainSingle().Which.Should().Be("true");
        response.Headers.GetValues("Link").Should().ContainSingle()
            .Which.Should().Be("<https://localhost:7230/1/manifests/no-here>; rel=\"successor-version\"");
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
    public async Task Get_HierarchicalManifest_Authorised_ProcessesInPlace_WithDeprecationHeaders()
    {
        // Arrange - carries an Authorization header, so this must not be redirected - processed in place against
        // the canonical host instead. StorageController.GetHierarchical still performs its own authorised
        // hierarchical -> flat 303 redirect, just now against the canonical host rather than the legacy one
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/iiif-manifest");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.Should().Be("https://localhost:7230/1/manifests/FirstChildManifest");
        response.Headers.GetValues("Deprecation").Should().ContainSingle().Which.Should().Be("true");
        response.Headers.GetValues("Link").Should().ContainSingle()
            .Which.Should().Be("<https://localhost:7230/1/iiif-manifest>; rel=\"successor-version\"");
        // No LegacyHostSunsetDate configured for this test class
        response.Headers.Should().NotContainKey("Sunset");
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

    [Fact]
    public async Task Get_Anonymous_RedirectResponse_HasNoDeprecationHeaders()
    {
        // Arrange - the deprecation-notice headers are only ever added on the in-place-processing branch (requests
        // carrying an Authorization header) - a plain redirect response should carry none of them
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/some/hierarchical/path");
        AddLegacyHostHeader(requestMessage);

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Should().NotContainKey("Deprecation");
        response.Headers.Should().NotContainKey("Sunset");
        response.Headers.Should().NotContainKey("Link");
    }
}

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class LegacyHostRedirectMiddlewareDeprecationDatesTests : IClassFixture<PresentationAppFactory<Program>>
{
    private const string LegacyHost = "legacy-with-dates.example.com";

    private readonly HttpClient httpClient;

    public LegacyHostRedirectMiddlewareDeprecationDatesTests(StorageFixture storageFixture,
        PresentationAppFactory<Program> factory)
    {
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture)
                .WithConfigValue("PathSettings:LegacyPresentationApiUrl", $"https://{LegacyHost}")
                .WithConfigValue("PathSettings:LegacyHostnameCutoffDate", "2026-01-01T00:00:00Z")
                .WithConfigValue("PathSettings:LegacyHostSunsetDate", "2027-01-01T00:00:00+00:00"));
        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task Get_Authorised_IncludesConfiguredDeprecationAndSunsetDates()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/iiif-manifest");
        requestMessage.Headers.Add("Host", LegacyHost);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert - both are HTTP-date (IMF-fixdate) formatted, converted to UTC. Deprecation (RFC 9745) carries
        // LegacyHostnameCutoffDate rather than the bare "true" now that a date is configured; Sunset (RFC 8594)
        // carries LegacyHostSunsetDate. Link is unaffected by either setting, but checked here too for completeness
        response.Headers.GetValues("Deprecation").Should().ContainSingle()
            .Which.Should().Be("Thu, 01 Jan 2026 00:00:00 GMT");
        response.Headers.GetValues("Sunset").Should().ContainSingle()
            .Which.Should().Be("Fri, 01 Jan 2027 00:00:00 GMT");
        response.Headers.GetValues("Link").Should().ContainSingle()
            .Which.Should().Be("<https://localhost:7230/1/iiif-manifest>; rel=\"successor-version\"");
    }
}

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class LegacyHostRedirectMiddlewareCutoffDateOnlyTests : IClassFixture<PresentationAppFactory<Program>>
{
    private const string LegacyHost = "legacy-with-cutoff-only.example.com";

    private readonly HttpClient httpClient;

    public LegacyHostRedirectMiddlewareCutoffDateOnlyTests(StorageFixture storageFixture,
        PresentationAppFactory<Program> factory)
    {
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture)
                .WithConfigValue("PathSettings:LegacyPresentationApiUrl", $"https://{LegacyHost}")
                // Deliberately no "Z"/offset suffix - exercises the DateTimeKind.Unspecified branch of
                // LegacyHostRedirectMiddleware.ToHttpDate (the "Z"/offset-suffixed form used elsewhere in this
                // file instead binds to DateTimeKind.Local, per config binder behaviour - both need covering)
                .WithConfigValue("PathSettings:LegacyHostnameCutoffDate", "2026-01-01T00:00:00"));
        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task Get_Authorised_IncludesCutoffDate_ButNoSunset()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/iiif-manifest");
        requestMessage.Headers.Add("Host", LegacyHost);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert - LegacyHostnameCutoffDate configured on its own: Deprecation carries it (still correctly
        // formatted despite the Unspecified Kind - assumed already-UTC), Sunset is absent (no LegacyHostSunsetDate)
        response.Headers.GetValues("Deprecation").Should().ContainSingle()
            .Which.Should().Be("Thu, 01 Jan 2026 00:00:00 GMT");
        response.Headers.Should().NotContainKey("Sunset");
    }
}

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class LegacyHostRedirectMiddlewareSunsetDateOnlyTests : IClassFixture<PresentationAppFactory<Program>>
{
    private const string LegacyHost = "legacy-with-sunset-only.example.com";

    private readonly HttpClient httpClient;

    public LegacyHostRedirectMiddlewareSunsetDateOnlyTests(StorageFixture storageFixture,
        PresentationAppFactory<Program> factory)
    {
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture)
                .WithConfigValue("PathSettings:LegacyPresentationApiUrl", $"https://{LegacyHost}")
                .WithConfigValue("PathSettings:LegacyHostSunsetDate", "2027-01-01T00:00:00+00:00"));
        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task Get_Authorised_IncludesSunsetDate_ButDeprecationStaysBare()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/iiif-manifest");
        requestMessage.Headers.Add("Host", LegacyHost);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert - LegacyHostSunsetDate configured on its own: Sunset carries it, Deprecation stays the bare
        // "true" (no LegacyHostnameCutoffDate configured here)
        response.Headers.GetValues("Deprecation").Should().ContainSingle().Which.Should().Be("true");
        response.Headers.GetValues("Sunset").Should().ContainSingle()
            .Which.Should().Be("Fri, 01 Jan 2027 00:00:00 GMT");
    }
}
