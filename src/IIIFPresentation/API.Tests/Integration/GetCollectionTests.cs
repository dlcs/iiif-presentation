using System.Net;
using API.Tests.Integration.Infrastucture;
using Core.Response;
using FluentAssertions;
using IIIF.Presentation.V3;
using Microsoft.AspNetCore.Mvc.Testing;
using Models.API.Collection;
using Test.Helpers.Integration;

#nullable disable

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
        
        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task Get_RootHierarchical_Returns_EntryPoint()
    {
        // Act
        var response = await httpClient.GetAsync("1");
        
        // Act
        var collection = await response.ReadAsIIIFJsonAsync<Collection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1");
        collection.Items.Count.Should().Be(2);
        var firstItem = (Collection)collection.Items[0];
        firstItem.Id.Should().Be("http://localhost/1/first-child");
    }
    
    [Fact]
    public async Task Get_ChildHierarchical_Returns_Child()
    {
        // Act
        var response = await httpClient.GetAsync("1/first-child");

        var collection = await response.ReadAsIIIFJsonAsync<Collection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1/first-child");
        collection.Items.Count.Should().Be(1);
        
        var firstItem = (Collection)collection.Items[0];
        firstItem.Id.Should().Be("http://localhost/1/first-child/second-child");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsEntryPointHierarchical_WhenNoAuthAndCsHeader()
    {
        // Act
        var response = await httpClient.GetAsync("1/collections/root");

        var collection = await response.ReadAsIIIFJsonAsync<Collection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1");
        collection.Items.Count.Should().Be(2);
        var firstItem = (Collection)collection.Items[0];
        firstItem.Id.Should().Be("http://localhost/1/first-child");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsEntryPointHierarchical_WhenNoCsHeader()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/root");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsIIIFJsonAsync<Collection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1");
        collection.Items.Count.Should().Be(2);
        var firstItem = (Collection)collection.Items[0];
        firstItem.Id.Should().Be("http://localhost/1/first-child");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsEntryPointHierarchical_WhenIncorrectCsHeader()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/root");
        requestMessage.Headers.Add("IIIF-CS-Show-Extra", "Incorrect");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsIIIFJsonAsync<Collection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1");
        collection.Items.Count.Should().Be(2);
        var firstItem = (Collection)collection.Items[0];
        firstItem.Id.Should().Be("http://localhost/1/first-child");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsEntryPointHierarchical_WhenNoAuth()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/root");
        requestMessage.AddPrivateHeaders();
    
        // Act
        var response = await httpClient.SendAsync(requestMessage);

        var collection = await response.ReadAsIIIFJsonAsync<Collection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1");
        collection.Items.Count.Should().Be(2);
        var firstItem = (Collection)collection.Items[0];
        firstItem.Id.Should().Be("http://localhost/1/first-child");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsEntryPointFlat_WhenAuthAndHeader()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/root");
        requestMessage.AddPrivateHeaders();

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<FlatCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1/collections/RootStorage");
        collection.PublicId.Should().Be("http://localhost/1");
        collection.Items!.Count.Should().Be(2);
        collection.Items[0].Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
        collection.TotalItems.Should().Be(2);
        collection.CreatedBy.Should().Be("admin");
        collection.Behavior.Should().Contain("public-iiif");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsEntryPointFlat_WhenCalledById()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/RootStorage");
        requestMessage.AddPrivateHeaders();

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<FlatCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1/collections/RootStorage");
        collection.PublicId.Should().Be("http://localhost/1");
        collection.Items!.Count.Should().Be(2);
        collection.Items[0].Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
        collection.TotalItems.Should().Be(2);
        collection.CreatedBy.Should().Be("admin");
        collection.Behavior.Should().Contain("public-iiif");
    }
    
    [Fact]
    public async Task Get_ChildFlat_ReturnsEntryPointFlat_WhenCalledByChildId()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/FirstChildCollection");
        requestMessage.AddPrivateHeaders();

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<FlatCollection>();

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
    
    [Fact]
    public async Task Get_PrivateChild_ReturnsCorrectlyFlatAndHierarchical()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/NonPublic");
        requestMessage.AddPrivateHeaders();

        // Act
        var flatResponse = await httpClient.AsCustomer(1).SendAsync(requestMessage);
        var hierarchicalResponse = await httpClient.AsCustomer(1).GetAsync("1/non-public");

        var flatCollection = await flatResponse.ReadAsPresentationJsonAsync<FlatCollection>();

        // Assert
        flatResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        flatCollection!.Id.Should().Be("http://localhost/1/collections/NonPublic");
        flatCollection.PublicId.Should().Be("http://localhost/1/non-public");
        flatCollection.Items!.Count.Should().Be(0);
        flatCollection.CreatedBy.Should().Be("admin");
        flatCollection.Behavior.Should().Contain("storage-collection");
        flatCollection.Behavior.Should().NotContain("public-iiif");
        hierarchicalResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}