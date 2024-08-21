using System.Net;
using System.Net.Http.Headers;
using API.Tests.Integration.Infrastucture;
using Core.Response;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Models.API.Collection;
using Repository;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class GetCollectionTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;

    public GetCollectionTests(PresentationContextFixture dbFixture, PresentationAppFactory<Program> factory)
    {
        httpClient = factory.WithConnectionString(dbFixture.ConnectionString)
            .CreateClient(new WebApplicationFactoryClientOptions());
    }
    
    [Fact]
    public async Task Get_RootHierarchical_Returns_EntryPoint()
    {
        // Act
        var response = await httpClient.GetAsync("1");

        var collection = await response.ReadAsJsonAsync<HierarchicalCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1");
        collection.Items.Count.Should().Be(1);
        collection.Items[0].Id.Should().Be("http://localhost/1/first-child");
    }
    
    [Fact]
    public async Task Get_ChildHierarchical_Returns_Child()
    {
        // Act
        var response = await httpClient.GetAsync("1/first-child");

        var collection = await response.ReadAsJsonAsync<HierarchicalCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1/first-child");
        collection.Items.Count.Should().Be(1);
        collection.Items[0].Id.Should().Be("http://localhost/1/first-child/second-child");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsEntryPointHierarchical_WhenNoAuthAndCsHeader()
    {
        // Act
        var response = await httpClient.GetAsync("1/collections/root");

        var collection = await response.ReadAsJsonAsync<HierarchicalCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1");
        collection.Items.Count.Should().Be(1);
        collection.Items[0].Id.Should().Be("http://localhost/1/first-child");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsEntryPointHierarchical_WhenNoCsHeader()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/root");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsJsonAsync<HierarchicalCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1");
        collection.Items.Count.Should().Be(1);
        collection.Items[0].Id.Should().Be("http://localhost/1/first-child");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsEntryPointHierarchical_WhenNoAuth()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/root");
        requestMessage.Headers.Add("IIIF-CS-Show-Extra", "value");
    
        // Act
        var response = await httpClient.SendAsync(requestMessage);

        var collection = await response.ReadAsJsonAsync<HierarchicalCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1");
        collection.Items.Count.Should().Be(1);
        collection.Items[0].Id.Should().Be("http://localhost/1/first-child");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsEntryPointFlat_WhenAuthAndHeader()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/root");
        requestMessage.Headers.Add("IIIF-CS-Show-Extra", "value");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsJsonAsync<FlatCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1/collections/RootStorage");
        collection.PublicId.Should().Be("http://localhost/1");
        collection.Items!.Count.Should().Be(1);
        collection.Items[0].Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
        collection.TotalItems.Should().Be(1);
        collection.CreatedBy.Should().Be("admin");
        collection.Behavior.Should().Contain("public-iiif");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsEntryPointFlat_WhenCalledById()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/RootStorage");
        requestMessage.Headers.Add("IIIF-CS-Show-Extra", "value");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsJsonAsync<FlatCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1/collections/RootStorage");
        collection.PublicId.Should().Be("http://localhost/1");
        collection.Items!.Count.Should().Be(1);
        collection.Items[0].Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
        collection.TotalItems.Should().Be(1);
        collection.CreatedBy.Should().Be("admin");
        collection.Behavior.Should().Contain("public-iiif");
    }
    
    [Fact]
    public async Task Get_ChildFlat_ReturnsEntryPointFlat_WhenCalledByChildId()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/FirstChildCollection");
        requestMessage.Headers.Add("IIIF-CS-Show-Extra", "value");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsJsonAsync<FlatCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
        collection.PublicId.Should().Be("http://localhost/1/first-child");
        collection.Items!.Count.Should().Be(1);
        collection.Items[0].Id.Should().Be("http://localhost/1/collections/SecondChildCollection");
        collection.TotalItems.Should().Be(1);
        collection.CreatedBy.Should().Be("admin");
        collection.Behavior.Should().Contain("public-iiif");
    }
}