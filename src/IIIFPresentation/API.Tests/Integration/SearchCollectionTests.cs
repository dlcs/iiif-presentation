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

    public SearchCollectionTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbFixture = storageFixture.DbFixture;
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(dbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));
        dbFixture.CleanUp();
    }

    private async Task SeedSearchCustomer()
    {
        var provider = new TestCustomerIdProvider();
        provider.SetCustomerId(SearchCustomer);
        await using var ctx = dbFixture.GetNewPresentationContext(provider);

        // CleanUp() preserves 'root' for all customers, so only add it if this is the first test to run
        if (!await ctx.Collections.AnyAsync(c => c.Id == RootCollection.Id))
        {
            await ctx.Collections.AddTestRootCollection(SearchCustomer);
            await ctx.SaveChangesAsync();
        }

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
    public async Task Search_ReturnsNotFound_WhenCollectionNotRoot()
    {
        // Act
        var response = await httpClient.AsCustomer().GetAsync("1/collections/not-root/search?label=medicine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_ReturnsBadRequest_WhenLabelMissing()
    {
        // Act
        var response = await httpClient.AsCustomer().GetAsync($"1/collections/{RootCollection.Id}/search");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("  ab  ")]
    public async Task Search_ReturnsBadRequest_WhenLabelBelowMinimumLength(string label)
    {
        // Act
        var response =
            await httpClient.AsCustomer().GetAsync($"1/collections/{RootCollection.Id}/search?label={label}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_ReturnsMatchingItems_UsingFlatIds()
    {
        // Arrange
        await SeedSearchCustomer();

        // Act
        var response = await httpClient.AsCustomer(SearchCustomer)
            .GetAsync($"{SearchCustomer}/collections/{RootCollection.Id}/search?label=hunter+thompson");
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be($"http://localhost/{SearchCustomer}/collections/{RootCollection.Id}/search");
        collection.SeeAlso.Should().ContainSingle().Which.Id.Should()
            .Be($"http://localhost/{SearchCustomer}/collections/{RootCollection.Id}", "links back to what was searched");
        collection.TotalItems.Should().Be(2, "'Emma Thompson' is not a match");

        collection.Items.OfType<Collection>().Single().Id.Should()
            .Be($"http://localhost/{SearchCustomer}/collections/hst-coll");
        collection.Items.OfType<Manifest>().Single().Id.Should()
            .Be($"http://localhost/{SearchCustomer}/manifests/hst-man");
    }

    [Fact]
    public async Task Search_ReturnsEmptyCollection_WhenNoMatches()
    {
        // Arrange
        await SeedSearchCustomer();

        // Act
        var response = await httpClient.AsCustomer(SearchCustomer)
            .GetAsync($"{SearchCustomer}/collections/{RootCollection.Id}/search?label=kerouac");
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.TotalItems.Should().Be(0);
        collection.Items.Should().BeNullOrEmpty("an empty items list isn't serialised");
    }

    [Theory]
    [InlineData("orderBy=id", "hst-coll", "hst-man")]
    [InlineData("orderByDescending=id", "hst-man", "hst-coll")]
    public async Task Search_OrdersResults(string orderQueryParam, string firstId, string secondId)
    {
        // Arrange
        await SeedSearchCustomer();

        // Act
        var response = await httpClient.AsCustomer(SearchCustomer).GetAsync(
            $"{SearchCustomer}/collections/{RootCollection.Id}/search?label=hunter+thompson&{orderQueryParam}");
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Items.OfType<ResourceBase>().Select(i => i.Id).Should()
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
        var response = await httpClient.AsCustomer(SearchCustomer).GetAsync(
            $"{SearchCustomer}/collections/{RootCollection.Id}/search?label=hunter+thompson&pageSize=1&orderByDescending=id");
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.View.Id.Should()
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
        var response = await httpClient.AsCustomer(SearchCustomer)
            .GetAsync($"{SearchCustomer}/collections/{RootCollection.Id}/search?label=hunter+thompson&pageSize=1");
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.View.Id.Should().Be($"{searchPath}?label=hunter%20thompson&page=1&pageSize=1");
        collection.View.TotalPages.Should().Be(2);
        collection.View.Next.Should().Be(new Uri($"{searchPath}?label=hunter%20thompson&page=2&pageSize=1"));
        collection.View.Last.Should().Be(new Uri($"{searchPath}?label=hunter%20thompson&page=2&pageSize=1"));
        collection.View.Previous.Should().BeNull();
    }
}
