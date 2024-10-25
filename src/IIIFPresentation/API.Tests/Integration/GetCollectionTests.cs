#nullable disable

using System.Net;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using IIIF.Presentation.V3;
using Models.API.Collection;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class GetCollectionTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private const int TotalDatabaseChildItems = 4;

    public GetCollectionTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));
        
        storageFixture.DbFixture.CleanUp();
    }
    
    [Fact]
    public async Task Get_RootHierarchical_Returns_EntryPoint()
    {
        // Act
        var response = await httpClient.GetAsync("1");
        
        // Act
        var collection = await response.ReadAsPresentationJsonAsync<Collection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1");
        collection.Items.Count.Should().Be(TotalDatabaseChildItems);
        var firstItem = (Collection)collection.Items[0];
        firstItem.Id.Should().Be("http://localhost/1/first-child");
    }
    
    [Fact]
    public async Task Get_ChildHierarchical_Returns_Child()
    {
        // Act
        var response = await httpClient.GetAsync("1/first-child");

        var collection = await response.ReadAsPresentationJsonAsync<Collection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1/first-child");
        collection.Items.Count.Should().Be(1);
        
        var firstItem = (Collection)collection.Items[0];
        firstItem.Id.Should().Be("http://localhost/1/first-child/second-child");
    }

    [Fact]
    public async Task Get_ChildHierarchical_Returns_Vary_Header()
    {
        // Act
        var response = await httpClient.GetAsync("1/first-child");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task Get_Hierarchical_ReturnsSeeOther_WhenAuthAndShowExtrasHeaders()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");
    }
    
    [Fact]
    public async Task Get_Hierarchical_ReturnsSeeOther_WithQueryParameters_WhenAuthAndShowExtrasHeadersAndQuery()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1?page=2&pageSize=2");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.Should().Be($"http://localhost/1/collections/{RootCollection.Id}?page=2&pageSize=2");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsSeeOther_WhenNoAuthOrCsHeader()
    {
        // Act
        var response = await httpClient.GetAsync($"1/collections/{RootCollection.Id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.Should().Be("http://localhost/1");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsSeeOther_WhenNoCsHeader()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"1/collections/{RootCollection.Id}");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.Should().Be("http://localhost/1");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsSeeOther_WhenShowExtraHeaderNotAll()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"1/collections/{RootCollection.Id}");
        requestMessage.Headers.Add("IIIF-CS-Show-Extra", "Incorrect");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.Should().Be("http://localhost/1");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsSeeOther_WhenNoAuth()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/collections/{RootCollection.Id}");
    
        // Act
        var response = await httpClient.SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.Should().Be("http://localhost/1");
    }

    [Fact]
    public async Task Get_RootFlat_ReturnsNotFound_WhenNoAuthOrCsHeaderAndNotPublic()
    {
        // Act
        var response = await httpClient.GetAsync($"2/collections/{RootCollection.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Get_RootFlat_ReturnsNotFound_WhenNoCsHeaderAndNotPublic()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"2/collections/{RootCollection.Id}");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Get_RootFlat_ReturnsNotFound_WhenNoAuthAndNotPublic()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"2/collections/{RootCollection.Id}");

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Location.Should().BeNull();
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsEntryPointFlat_WhenAuthAndHeader()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/collections/{RootCollection.Id}");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");
        collection.PublicId.Should().Be("http://localhost/1");
        collection.Items!.Count.Should().Be(TotalDatabaseChildItems);
        collection.Items.OfType<Collection>().First().Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
        collection.TotalItems.Should().Be(TotalDatabaseChildItems);
        collection.CreatedBy.Should().Be("admin");
        collection.Behavior.Should().Contain("public-iiif");
    }
    
    [Fact]
    public async Task Get_ChildFlat_ReturnsEntryPointFlat_WhenCalledByChildId()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/collections/FirstChildCollection");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
        collection.PublicId.Should().Be("http://localhost/1/first-child");
        collection.Items!.Count.Should().Be(1);
        collection.Items.OfType<Collection>().First().Id.Should().Be("http://localhost/1/collections/SecondChildCollection");
        collection.TotalItems.Should().Be(1);
        collection.CreatedBy.Should().Be("admin");
        collection.Behavior.Should().Contain("public-iiif");
        collection.Parent.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");
    }
    
    [Fact]
    public async Task Get_PrivateChild_ReturnsCorrectlyFlatAndHierarchical()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/collections/NonPublic");

        // Act
        var flatResponse = await httpClient.AsCustomer(1).SendAsync(requestMessage);
        var hierarchicalResponse = await httpClient.AsCustomer(1).GetAsync("1/non-public");

        var flatCollection = await flatResponse.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        flatResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        flatCollection!.Id.Should().Be("http://localhost/1/collections/NonPublic");
        flatCollection.PublicId.Should().Be("http://localhost/1/non-public");
        flatCollection.Items!.Count.Should().Be(0);
        flatCollection.CreatedBy.Should().Be("admin");
        flatCollection.Behavior.Should().Contain("storage-collection");
        flatCollection.Behavior.Should().NotContain("public-iiif");
        flatCollection.Parent.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");
        hierarchicalResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_RootFlat_ReturnsItems_WhenCalledWithPageSize()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/collections/{RootCollection.Id}?page=1&pageSize=100");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();
        
        // Assert
        collection.TotalItems.Should().Be(TotalDatabaseChildItems);
        collection.View!.PageSize.Should().Be(100);
        collection.View.Page.Should().Be(1);
        collection.View.TotalPages.Should().Be(1);
        collection.Items!.Count.Should().Be(TotalDatabaseChildItems);
    }
    
    [Fact]
    public async Task Get_ChildFlat_ReturnsReducedItems_WhenCalledWithSmallPageSize()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/collections/{RootCollection.Id}?page=1&pageSize=1");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();
        
        // Assert
        collection.TotalItems.Should().Be(TotalDatabaseChildItems);
        collection.View!.PageSize.Should().Be(1);
        collection.View.Page.Should().Be(1);
        collection.View.TotalPages.Should().Be(TotalDatabaseChildItems);
        collection.Items!.Count.Should().Be(1);
        collection.Items.OfType<Collection>().First().Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
    }
    
    [Fact]
    public async Task Get_RootFlat_CorrectPublicId()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/collections/{RootCollection.Id}");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();
        
        // Assert
        collection.PublicId.Should().Be("http://localhost/1");
        collection.Items.OfType<Collection>().First().Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
    }
    
    [Fact]
    public async Task Get_ChildFlat_CorrectPublicId()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/collections/FirstChildCollection");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();
        
        // Assert
        collection.PublicId.Should().Be("http://localhost/1/first-child");
        collection.Items.OfType<Collection>().First().Id.Should().Be("http://localhost/1/collections/SecondChildCollection");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsSecondPage_WhenCalledWithSmallPageSize()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/collections/{RootCollection.Id}?page=2&pageSize=1");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();
        
        // Assert
        collection.TotalItems.Should().Be(TotalDatabaseChildItems);
        collection.View!.PageSize.Should().Be(1);
        collection.View.Page.Should().Be(2);
        collection.View.TotalPages.Should().Be(TotalDatabaseChildItems);
        collection.Items!.Count.Should().Be(1);
        collection.Items.OfType<Collection>().First().Id.Should().Be("http://localhost/1/collections/NonPublic");
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsDefaults_WhenCalledWithZeroPage()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/collections/{RootCollection.Id}?page=0&pageSize=0");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();
        
        // Assert
        collection.TotalItems.Should().Be(TotalDatabaseChildItems);
        collection.View!.PageSize.Should().Be(20);
        collection.View.Page.Should().Be(1);
        collection.View.TotalPages.Should().Be(1);
        collection.Items!.Count.Should().Be(TotalDatabaseChildItems);
    }
    
    [Fact]
    public async Task Get_RootFlat_ReturnsMaxPageSize_WhenCalledPageSizeExceedsMax()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/collections/{RootCollection.Id}?page=1&pageSize=1000");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();
        
        // Assert
        collection.TotalItems.Should().Be(TotalDatabaseChildItems);
        collection.View!.PageSize.Should().Be(100);
        collection.View.Page.Should().Be(1);
        collection.View.TotalPages.Should().Be(1);
        collection.Items!.Count.Should().Be(TotalDatabaseChildItems);
    }
    
    [Theory]
    [InlineData("id")]
    [InlineData("slug")]
    [InlineData("created")]
    public async Task Get_ChildFlat_ReturnsCorrectItem_WhenCalledWithSmallPageSizeAndOrderBy(string field)
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"1/collections/{RootCollection.Id}?page=1&pageSize=1&orderBy={field}");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();
        
        // Assert
        collection.TotalItems.Should().Be(TotalDatabaseChildItems);
        collection.View!.PageSize.Should().Be(1);
        collection.View.Page.Should().Be(1);
        collection.View.TotalPages.Should().Be(TotalDatabaseChildItems);
        collection.Items!.Count.Should().Be(1);
        
        collection.Items.OfType<Collection>().First().Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
    }
    
    [Theory]
    [InlineData("id", "collections/NonPublic")]
    [InlineData("slug", "collections/NonPublic")]
    [InlineData("created", "manifests/FirstChildManifest")]
    public async Task Get_RootFlat_ReturnsFirstPageWithSecondItem_WhenCalledWithSmallPageSizeAndOrderByDescending(string field, string expectedItemId)
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"1/collections/{RootCollection.Id}?page=1&pageSize=1&orderByDescending={field}");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();
        
        // Assert
        collection.TotalItems.Should().Be(TotalDatabaseChildItems);
        collection.View!.PageSize.Should().Be(1);
        collection.View.Id.Should().Be($"http://localhost/1/collections/{RootCollection.Id}?page=1&pageSize=1&orderByDescending={field}");
        collection.View.Page.Should().Be(1);
        collection.View.TotalPages.Should().Be(TotalDatabaseChildItems);
        collection.Items!.Count.Should().Be(1);
        
        collection.Items.OfType<ResourceBase>().First().Id.Should().Be($"http://localhost/1/{expectedItemId}");
    }
    
    [Fact]
    public async Task Get_ChildFlat_IgnoresOrderBy_WhenCalledWithInvalidOrderBy()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"1/collections/{RootCollection.Id}?page=1&pageSize=1&orderByDescending=notValid");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();
        
        // Assert
        collection.TotalItems.Should().Be(TotalDatabaseChildItems);
        collection.View!.PageSize.Should().Be(1);
        collection.View.Id.Should().Be($"http://localhost/1/collections/{RootCollection.Id}?page=1&pageSize=1");
        collection.View.Page.Should().Be(1);
        collection.View.TotalPages.Should().Be(TotalDatabaseChildItems);
        collection.Items!.Count.Should().Be(1);
        collection.Items.OfType<Collection>().First().Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
    }

    [Fact]
    public async Task Get_ChildFlat_Returns_Vary_Header()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"1/collections/{RootCollection.Id}");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task Get_IiifCollection_ReturnsCollectionFromS3()
    {
        // Arrange and Act
        var response = await httpClient.GetAsync("1/iiif-collection");
        
        var collection = await response.ReadAsPresentationJsonAsync<Collection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().HaveCount(2);
        collection!.Items.Should().BeNull();
        collection.Behavior![0].Should().Be("public-iiif");
        collection.Type.Should().Be("Collection");
    }
}