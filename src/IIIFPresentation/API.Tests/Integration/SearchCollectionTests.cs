#nullable disable

using System.Net;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Models.API.Collection;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using Collection = IIIF.Presentation.V3.Collection;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class SearchCollectionTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContextFixture dbFixture;

    /// <summary>Search results are seeded under their own customer, so they don't leak into other test classes</summary>
    private const int SearchCustomer = 631;

    /// <summary>A second customer, holding resources that match the same search terms as <see cref="SearchCustomer"/></summary>
    private const int OtherCustomer = 632;

    public SearchCollectionTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbFixture = storageFixture.DbFixture;
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(dbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));
        dbFixture.CleanUp();
    }

    /// <summary>
    /// Seeds resources under <see cref="OtherCustomer"/> that match the same terms as those under
    /// <see cref="SearchCustomer"/>, so a search that isn't customer-scoped would pick them up
    /// </summary>
    private async Task SeedOtherCustomer()
    {
        await using var ctx = await GetSeededContext(OtherCustomer);

        var hunterCollection =
            (await ctx.Collections.AddTestCollection(id: "other-hst-coll", customer: OtherCustomer)).Entity;
        hunterCollection.Label = new LanguageMap("en", ["Hunter S. Thompson"]);

        await ctx.Manifests.AddTestManifest(id: "other-hst-man", customer: OtherCustomer,
            label: new LanguageMap("en", ["Thompson, Hunter"]));

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    /// Gets a context scoped to the customer, ensuring they have the root collection they'd get on creation.
    /// CleanUp() preserves 'root' for all customers, so this is idempotent across tests sharing the fixture.
    /// </summary>
    private async Task<PresentationContext> GetSeededContext(int customer)
    {
        var provider = new TestCustomerIdProvider();
        provider.SetCustomerId(customer);
        var ctx = dbFixture.GetNewPresentationContext(provider);

        if (!await ctx.Collections.AnyAsync(c => c.Id == RootCollection.Id))
        {
            await ctx.Collections.AddTestRootCollection(customer);
            await ctx.SaveChangesAsync();
        }

        return ctx;
    }

    private async Task SeedSearchCustomer()
    {
        await using var ctx = await GetSeededContext(SearchCustomer);

        var hunterCollection =
            (await ctx.Collections.AddTestCollection(id: "hst-coll", customer: SearchCustomer)).Entity;
        hunterCollection.Label = new LanguageMap("en", ["Hunter S. Thompson"]);

        await ctx.Manifests.AddTestManifest(id: "hst-man", customer: SearchCustomer,
            label: new LanguageMap("en", ["Thompson, Hunter"]));

        var emmaCollection =
            (await ctx.Collections.AddTestCollection(id: "emma-coll", customer: SearchCustomer)).Entity;
        emmaCollection.Label = new LanguageMap("en", ["Emma Thompson"]);

        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Search_ReturnsUnauthorized_WhenCalledWithoutAuth()
    {
        // Act
        var response = await httpClient.GetAsync($"1/collections/{RootCollection.Id}/search?label=medicine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task Search_ReturnsForbidden_WhenCalledWithoutShowExtras()
    {
        // Act
        var response = await httpClient.AsCustomer().GetAsync($"1/collections/{RootCollection.Id}/search?label=medicine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.Vary.Should().Contain("X-IIIF-CS-Show-Extras",
            "the 403 is a function of the header, so must not be cached in place of a 200");
    }

    [Fact]
    public async Task Search_ReturnsNotFound_WhenCollectionNotRoot()
    {
        // Act
        var request =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/collections/not-root/search?label=medicine"); 
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_ReturnsBadRequest_WhenLabelMissing()
    {
        // Act
        var request =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/collections/{RootCollection.Id}/search"); 
        var response = await httpClient.AsCustomer().SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("  ab  ")]
    [InlineData("a b")] // no single term is selective, despite the label as a whole being over the minimum length
    [InlineData("  a   b  ")]
    public async Task Search_ReturnsBadRequest_WhenLabelBelowMinimumLength(string label)
    {
        // Act
        var request =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"1/collections/{RootCollection.Id}/search?label={Uri.EscapeDataString(label)}"); 
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_ReturnsMatchingItems_UsingFlatIds()
    {
        // Arrange
        await SeedSearchCustomer();

        // Act
        var request =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{SearchCustomer}/collections/{RootCollection.Id}/search?label=hunter+thompson");
        var response = await httpClient.AsCustomer(SearchCustomer).SendAsync(request);
        var rawBody = await response.Content.ReadAsStringAsync();
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        rawBody.Should().NotContain("\"created\"", "audit props are meaningless on a synthetic search collection")
            .And.NotContain("\"modified\"");
        collection!.Id.Should().Be($"http://localhost/{SearchCustomer}/collections/{RootCollection.Id}/search");
        collection.SeeAlso.Should().ContainSingle().Which.Id.Should()
            .Be($"http://localhost/{SearchCustomer}/collections/{RootCollection.Id}", "links back to what was searched");
        collection.TotalItems.Should().Be(2, "'Emma Thompson' is not a match");

        collection.Items!.OfType<Collection>().Single().Id.Should()
            .Be($"http://localhost/{SearchCustomer}/collections/hst-coll");
        collection.Items.OfType<Manifest>().Single().Id.Should()
            .Be($"http://localhost/{SearchCustomer}/manifests/hst-man");
    }

    [Fact]
    public async Task Search_AllowsShortTerm_WhenAnotherTermIsLongEnough()
    {
        // Arrange
        await SeedSearchCustomer();

        // Act - "s." is under the minimum length, but "thompson" makes the search selective
        var request =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{SearchCustomer}/collections/{RootCollection.Id}/search?label=thompson+s.");
        var response = await httpClient.AsCustomer(SearchCustomer).SendAsync(request);
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.TotalItems.Should().Be(1, "the short term still narrows the search - 'Thompson, Hunter' has no 's.'");
        collection.Items!.OfType<Collection>().Single().Id.Should()
            .Be($"http://localhost/{SearchCustomer}/collections/hst-coll");
    }

    [Fact]
    public async Task Search_ReturnsEmptyCollection_WhenNoMatches()
    {
        // Arrange
        await SeedSearchCustomer();

        // Act
        var request =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{SearchCustomer}/collections/{RootCollection.Id}/search?label=kerouac");
        var response = await httpClient.AsCustomer(SearchCustomer).SendAsync(request);
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.TotalItems.Should().Be(0);
        collection.Items.Should().BeNullOrEmpty("an empty items list isn't serialised");
    }

    [Fact]
    public async Task Search_OnlyReturnsItemsForAuthenticatedCustomer()
    {
        // Arrange - both customers have resources matching 'hunter thompson'
        await SeedSearchCustomer();
        await SeedOtherCustomer();

        // Act - each customer searches their own root for the terms both of them match
        var searchCustomerResults = await SearchAsCustomer(SearchCustomer);
        var otherCustomerResults = await SearchAsCustomer(OtherCustomer);

        // Assert - each sees only their own 2 matches. Asserting both directions means a seed that silently
        // did nothing can't pass this test
        searchCustomerResults.Should().HaveCount(2, "the other customer's matches are not visible")
            .And.OnlyContain(id => id.Contains($"/{SearchCustomer}/"));
        otherCustomerResults.Should().HaveCount(2, "the other customer has matches of their own to leak")
            .And.OnlyContain(id => id.Contains($"/{OtherCustomer}/"));
        return;

        async Task<IEnumerable<string>> SearchAsCustomer(int customer)
        {
            var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{customer}/collections/{RootCollection.Id}/search?label=hunter+thompson");
            var response = await httpClient.AsCustomer(customer).SendAsync(request);
            var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return collection!.Items!.OfType<ResourceBase>().Select(i => i.Id);
        }
    }

    [Theory]
    [InlineData("orderBy=id", "hst-coll", "hst-man")]
    [InlineData("orderByDescending=id", "hst-man", "hst-coll")]
    public async Task Search_OrdersResults(string orderQueryParam, string firstId, string secondId)
    {
        // Arrange
        await SeedSearchCustomer();

        // Act
        var request =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{SearchCustomer}/collections/{RootCollection.Id}/search?label=hunter+thompson&{orderQueryParam}");
        var response = await httpClient.AsCustomer(SearchCustomer).SendAsync(request);
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Items!.OfType<ResourceBase>().Select(i => i.Id).Should()
            .ContainInOrder(GetFlatId(firstId), GetFlatId(secondId));
        return;

        string GetFlatId(string id) => id.EndsWith("-man")
            ? $"http://localhost/{SearchCustomer}/manifests/{id}"
            : $"http://localhost/{SearchCustomer}/collections/{id}";
    }

    [Fact]
    public async Task Search_ViewPreservesOrdering()
    {
        // Arrange
        await SeedSearchCustomer();
        var searchPath = $"http://localhost/{SearchCustomer}/collections/{RootCollection.Id}/search";

        // Act - pageSize of 1 forces a second page, so paging links are generated
        var request =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{SearchCustomer}/collections/{RootCollection.Id}/search?label=hunter+thompson&pageSize=1&orderByDescending=id");
        var response = await httpClient.AsCustomer(SearchCustomer).SendAsync(request);
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.View!.Id.Should()
            .Be($"{searchPath}?label=hunter%20thompson&page=1&pageSize=1&orderByDescending=id");
        collection.View.Next.Should()
            .Be(new Uri($"{searchPath}?label=hunter%20thompson&page=2&pageSize=1&orderByDescending=id"));
    }

    [Fact]
    public async Task Search_ViewPointsAtSearchEndpoint_PreservingSearchTerm()
    {
        // Arrange
        await SeedSearchCustomer();
        var searchPath = $"http://localhost/{SearchCustomer}/collections/{RootCollection.Id}/search";

        // Act - pageSize of 1 forces a second page
        var request =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{SearchCustomer}/collections/{RootCollection.Id}/search?label=hunter+thompson&pageSize=1");
        var response = await httpClient.AsCustomer(SearchCustomer).SendAsync(request);
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.View!.Id.Should().Be($"{searchPath}?label=hunter%20thompson&page=1&pageSize=1");
        collection.View.TotalPages.Should().Be(2);
        collection.View.Next.Should().Be(new Uri($"{searchPath}?label=hunter%20thompson&page=2&pageSize=1"));
        collection.View.Last.Should().Be(new Uri($"{searchPath}?label=hunter%20thompson&page=2&pageSize=1"));
        collection.View.Previous.Should().BeNull();
    }
}
