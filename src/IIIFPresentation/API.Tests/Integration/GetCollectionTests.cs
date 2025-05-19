#nullable disable

using System.Net;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using IIIF.Presentation.V3;
using Models.API.Collection;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class GetCollectionTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private const int TotalDatabaseChildItems = 6;
    private readonly IAmazonS3 amazonS3;

    public GetCollectionTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
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
        collection.Items.Count.Should().Be(TotalDatabaseChildItems - 2, "Two child items are non-public");
        var firstItem = (Collection)collection.Items[0];
        firstItem.Id.Should().Be("http://localhost/1/first-child");
        firstItem.Behavior.Should().BeNull();
        var secondItem = (Collection)collection.Items[1];
        secondItem.Id.Should().Be("http://localhost/1/iiif-collection");
        secondItem.Behavior.Should().BeNull();
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
    public async Task Get_ChildHierarchical_ReturnsNotFound_IfNotPublic()
    {
        // Act
        var response = await httpClient.GetAsync("1/non-public");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_Hierarchical_ReturnsSeeOther_WhenAuthAndShowExtrasHeaders()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
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
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
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
    public async Task Get_RootFlat_ReturnsCorrectSeeOther_WhenSecondCustomer()
    {
        // Act
        var response = await httpClient.GetAsync($"10/collections/{RootCollection.Id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.Should().Be("http://localhost/10");
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
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
        var expectedTotals = new DescendantCounts(2, 2, 2); 

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");
        collection.FlatId.Should().Be(RootCollection.Id);
        collection.PublicId.Should().Be("http://localhost/1");
        collection.Items!.Count.Should().Be(TotalDatabaseChildItems);
        collection.Items.OfType<Collection>().First().Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
        collection.TotalItems.Should().Be(TotalDatabaseChildItems);
        collection.CreatedBy.Should().Be("admin");
        collection.Behavior.Should().Contain("public-iiif");
        collection.PartOf.Should().BeNull("Root has no parent");
        collection.Totals.Should().BeEquivalentTo(expectedTotals);
        var firstItem = (Collection)collection.Items[0];
        firstItem.Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
        firstItem.Behavior.Should().Contain("public-iiif");
        firstItem.Behavior.Should().Contain("storage-collection");
        var secondItem = (Collection)collection.Items[1];
        secondItem.Id.Should().Be("http://localhost/1/collections/NonPublic");
        secondItem.Behavior.Should().NotContain("public-iiif");
        secondItem.Behavior.Should().Contain("storage-collection");
        var thirdItem = (Collection)collection.Items[2];
        thirdItem.Id.Should().Be("http://localhost/1/collections/IiifCollection");
        thirdItem.Behavior.Should().Contain("public-iiif");
        thirdItem.Behavior.Should().NotContain("storage-collection");
        var fifthItem = (Manifest)collection.Items[4];
        fifthItem.Id.Should().Be("http://localhost/1/manifests/FirstChildManifest");
    }
    
    [Fact]
    public async Task Get_ChildFlat_ReturnsEntryPointFlat_WhenCalledByChildId()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/collections/FirstChildCollection");
        var expectedTotals = new DescendantCounts(1, 0, 0); 

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://localhost/1/collections/FirstChildCollection");
        collection.FlatId.Should().Be("FirstChildCollection");
        collection.PublicId.Should().Be("http://localhost/1/first-child");
        collection.Items!.Count.Should().Be(1);
        collection.Items.OfType<Collection>().First().Id.Should().Be("http://localhost/1/collections/SecondChildCollection");
        collection.TotalItems.Should().Be(1);
        collection.CreatedBy.Should().Be("admin");
        collection.Behavior.Should().Contain("public-iiif");
        collection.Parent.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");
        collection.PartOf.Single().Id.Should().Be("http://localhost/1/collections/root");
        collection.Totals.Should().BeEquivalentTo(expectedTotals);
    }
    
    [Fact]
    public async Task Get_PrivateChild_ReturnsCorrectlyFlat()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/collections/NonPublic");

        // Act
        var flatResponse = await httpClient.AsCustomer().SendAsync(requestMessage);
        var flatCollection = await flatResponse.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        flatResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        flatCollection!.Id.Should().Be("http://localhost/1/collections/NonPublic");
        flatCollection.FlatId.Should().Be("NonPublic");
        flatCollection.PublicId.Should().Be("http://localhost/1/non-public");
        flatCollection.Items.Should().BeNull("There are no children");
        flatCollection.CreatedBy.Should().Be("admin");
        flatCollection.Behavior.Should().Contain("storage-collection");
        flatCollection.Behavior.Should().NotContain("public-iiif");
        flatCollection.Parent.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");
        flatCollection.PartOf.Single().Id.Should().Be("http://localhost/1/collections/root");
    }
    
    [Fact]
    public async Task Get_PrivateChild_Returns404Hierarchical()
    {
        // Act
        var hierarchicalResponse = await httpClient.AsCustomer().GetAsync("1/non-public");

        // Assert
        hierarchicalResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_RootFlat_ReturnsItems_WhenCalledWithPageSize()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"1/collections/{RootCollection.Id}?page=1&pageSize=100");

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/collections/FirstChildCollection");

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
    [InlineData("created", "manifests/FirstChildManifestProcessing")]
    public async Task Get_RootFlat_ReturnsFirstPageWithSecondItem_WhenCalledWithSmallPageSizeAndOrderByDescending(string field, string expectedItemId)
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"1/collections/{RootCollection.Id}?page=1&pageSize=1&orderByDescending={field}");

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
    public async Task Get_ChildFlat_CorrectTotalItems_WhenPageOutOfBounds()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"1/collections/{RootCollection.Id}?page=7&pageSize=15");

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        collection.TotalItems.Should().Be(TotalDatabaseChildItems);
        collection.View!.PageSize.Should().Be(15);
        collection.View.Page.Should().Be(7);
        collection.View.TotalPages.Should().Be(1);
        collection.Items.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Get_ChildFlat_Returns_Vary_Header()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"1/collections/{RootCollection.Id}");

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task Get_FlatIIIFCollection_ReturnsNullItemsWhenNoBackingCollection()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                "1/collections/IiifCollection");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Totals.Should().BeNull("IIIF Collections have no children");
        collection.Items.Should().BeNull();
        collection.View.Should().BeNull();
        collection.TotalItems.Should().BeNull();
        collection.Totals.Should().BeNull();
        collection.PublicId.Should().Be("http://localhost/1/iiif-collection");
        collection.Id.Should().Be("http://localhost/1/collections/IiifCollection");
        collection.Items.Should().BeNull();
    }
    
    [Fact]
    public async Task Get_FlatIIIFCollectionWithItems_ReturnsItemsCorrectly()
    {
        // Arrange
        var url = "1/collections/IiifCollectionWithItems";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, url);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Totals.Should().BeNull("IIIF Collections have no children");
        collection.Items.Should().HaveCount(2);
        collection.View.Should().BeNull();
        collection.TotalItems.Should().BeNull();
        collection.PublicId.Should().Be("http://localhost/1/iiif-collection-with-items");
        collection.Id.Should().Be($"http://localhost/{url}");
    }
    
    [Fact]
    public async Task Get_HierarchicalIIIFCollection_ReturnsCollectionFromS3()
    {
        // Arrange and Act
        var response = await httpClient.GetAsync("1/iiif-collection");
        
        var collection = await response.ReadAsPresentationJsonAsync<Collection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().HaveCount(2);
        collection!.Items.Should().BeNull();
        collection.Id.Should().Be("http://localhost/1/iiif-collection");
        collection.Behavior![0].Should().Be("public-iiif");
        collection.Type.Should().Be("Collection");
    }
    
    [Fact]
    public async Task Get_Hierarchical_ReturnsSeeOtherWithRewrittenPath_WhenAuthAndShowExtrasHeadersWithPathRewrites()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1");
        HttpRequestMessageBuilder.AddHostExampleHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.Should().Be("http://example.com/foo/1/collections/root");
    }
    
    [Fact]
    public async Task Get_Flat_ReturnsSeeOtherWithRewrittenPath_WhenNoShowExtrasHeadersWithPathRewrites()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1/collections/root");
        HttpRequestMessageBuilder.AddHostExampleHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.Should().Be("http://example.com/example/1");
    }
    
    [Fact]
    public async Task Get_Hierarchical_ReturnsWithRewrittenPaths_WhenAuthAndShowExtrasHeadersWithPathRewrites()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "1");
        HttpRequestMessageBuilder.AddHostExampleHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be("http://example.com/example/1");
        collection.Items.Should().HaveCount(TotalDatabaseChildItems - 2, "Two child items are non-public");
        var firstItem = (Collection)collection.Items[0];
        firstItem.Id.Should().Be("http://example.com/example/1/first-child");
        firstItem.Behavior.Should().BeNull();
        var secondItem = (Collection)collection.Items[1];
        secondItem.Id.Should().Be("http://example.com/example/1/iiif-collection");
        secondItem.Behavior.Should().BeNull();
    }
    
    [Fact]
    public async Task Get_Flat_ReturnsWithRewrittenPaths_WhenAuthAndShowExtrasHeadersWithPathRewrites()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/collections/root");
        HttpRequestMessageBuilder.AddHostExampleHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        var collection = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection!.Id.Should().Be($"http://example.com/foo/1/collections/{RootCollection.Id}");
        collection.FlatId.Should().Be(RootCollection.Id);
        collection.PublicId.Should().Be("http://example.com/example/1");
        collection.Items.Should().HaveCount(TotalDatabaseChildItems);
        collection.Items.OfType<Collection>().First().Id.Should().Be("http://example.com/foo/1/collections/FirstChildCollection");
        collection.TotalItems.Should().Be(TotalDatabaseChildItems);
        collection.CreatedBy.Should().Be("admin");
        collection.Behavior.Should().Contain("public-iiif");
        collection.PartOf.Should().BeNull("Root has no parent");
        var firstItem = (Collection)collection.Items[0];
        firstItem.Id.Should().Be("http://example.com/foo/1/collections/FirstChildCollection");
        firstItem.Behavior.Should().Contain("public-iiif");
        firstItem.Behavior.Should().Contain("storage-collection");
        var secondItem = (Collection)collection.Items[1];
        secondItem.Id.Should().Be("http://example.com/foo/1/collections/NonPublic");
        secondItem.Behavior.Should().NotContain("public-iiif");
        secondItem.Behavior.Should().Contain("storage-collection");
        var thirdItem = (Collection)collection.Items[2];
        thirdItem.Id.Should().Be("http://example.com/foo/1/collections/IiifCollection");
        thirdItem.Behavior.Should().Contain("public-iiif");
        thirdItem.Behavior.Should().NotContain("storage-collection");
        var fifthItem = (Manifest)collection.Items[4];
        fifthItem.Id.Should().Be("http://example.com/example/1/manifests/FirstChildManifest");
    }
}
