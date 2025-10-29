using System.Net;
using API.Tests.Integration.Infrastructure;
using Core.Infrastructure;
using Core.Response;
using DLCS.API;
using DLCS.Models;
using FakeItEasy;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.Extensions.DependencyInjection;
using Models.API.Collection;
using Models.API.Manifest;
using Newtonsoft.Json.Linq;
using Repository;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyRewrittenPathTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private readonly string parent;
    private const int Customer = 1;
    private readonly IDlcsApiClient dlcsApiClient;
    private const int NewlyCreatedSpace = 900;
    
    public ModifyRewrittenPathTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        
        dlcsApiClient = A.Fake<IDlcsApiClient>();
        A.CallTo(() => dlcsApiClient.CreateSpace(Customer, A<string>._, A<CancellationToken>._))
            .Returns(new Space { Id = NewlyCreatedSpace, Name = "test" });
        
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services => services.AddSingleton(dlcsApiClient));
        
        parent = dbContext.Collections
            .First(x => x.CustomerId == Customer && x.Hierarchy!.Any(h => h.Slug == string.Empty)).Id;

        storageFixture.DbFixture.CleanUp();
    }
    
    [Fact]
    public async Task CreateCollection_CreatesCollection_WhenRewrittenPaths()
    {
        var slug = TestIdentifiers.Id();
        var requestParent = $"http://no-customer.com/collections/{RootCollection.Id}";
        // Arrange
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", ["test collection"]),
            Slug = slug,
            Parent = requestParent,
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());
        HttpRequestMessageBuilder.AddHostNoCustomerHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        responseCollection.Parent.Should().Be(requestParent);
        responseCollection.PublicId.Should().Be($"http://no-customer.com/{slug}");
        hierarchyFromDatabase.Parent.Should().Be(parent);
        hierarchyFromDatabase.Slug.Should().Be(slug);
    }
    
    [Fact]
    public async Task CreateCollection_CreatesCollection_WhenRewrittenPathsWithAdditionalPathElement()
    {
        var slug = TestIdentifiers.Id();
        var requestParent = $"http://example.com/foo/{Customer}/collections/{RootCollection.Id}";
        // Arrange
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", ["test collection"]),
            Slug = slug,
            Parent = requestParent
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());
        HttpRequestMessageBuilder.AddHostExampleHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        responseCollection.Parent.Should().Be(requestParent);
        responseCollection.PublicId.Should().Be($"http://example.com/example/{Customer}/{slug}");
        hierarchyFromDatabase.Parent.Should().Be(parent);
        hierarchyFromDatabase.Slug.Should().Be(slug);
    }
    
    [Fact]
    public async Task CreateCollection_CreatesCollection_WhenRewrittenPathsWithPublicParentId()
    {
        var slug = TestIdentifiers.Id();
        // Arrange
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", ["test collection"]),
            Slug = slug,
            Parent = $"http://no-customer.com",
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());
        HttpRequestMessageBuilder.AddHostNoCustomerHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        responseCollection.Parent.Should().Be($"http://no-customer.com/collections/{RootCollection.Id}");
        responseCollection.PublicId.Should().Be($"http://no-customer.com/{slug}");
        hierarchyFromDatabase.Parent.Should().Be(parent);
        hierarchyFromDatabase.Slug.Should().Be(slug);
    }
    
    [Fact]
    public async Task CreateCollection_CreatesCollection_WhenRewrittenPathsWithPublicParentIdAndAdditionalPathElement()
    {
        var slug = TestIdentifiers.Id();
        // Arrange
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", ["test collection"]),
            Slug = slug,
            Parent = "http://example.com/example/1"
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());
        HttpRequestMessageBuilder.AddHostExampleHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        responseCollection.Parent.Should().Be($"http://example.com/foo/{Customer}/collections/{RootCollection.Id}");
        responseCollection.PublicId.Should().Be($"http://example.com/example/{Customer}/{slug}");
        hierarchyFromDatabase.Parent.Should().Be(parent);
        hierarchyFromDatabase.Slug.Should().Be(slug);
    }
    
    [Fact]
    public async Task CreateCollection_CreatesCollection_WhenRewrittenPathsWithPublicId()
    {
        var slug = TestIdentifiers.Id();
        // Arrange
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", ["test collection"]),
            PublicId = $"http://no-customer.com/{slug}"
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());
        HttpRequestMessageBuilder.AddHostNoCustomerHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();
        
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        responseCollection.Parent.Should().Be($"http://no-customer.com/collections/{RootCollection.Id}");
        responseCollection.PublicId.Should().Be($"http://no-customer.com/{slug}");
        hierarchyFromDatabase.Parent.Should().Be(parent);
        hierarchyFromDatabase.Slug.Should().Be(slug);
    }
    
    [Fact]
    public async Task CreateCollection_CreatesCollection_WhenRewrittenPathsWithPublicIdAndAdditionalPathElement()
    {
        var slug = TestIdentifiers.Id();
        // Arrange
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", ["test collection"]),
            PublicId = $"http://example.com/example/1/{slug}"
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());
        HttpRequestMessageBuilder.AddHostExampleHeader(requestMessage);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        responseCollection.Parent.Should().Be($"http://example.com/foo/{Customer}/collections/{RootCollection.Id}");
        responseCollection.PublicId.Should().Be($"http://example.com/example/{Customer}/{slug}");
        hierarchyFromDatabase.Parent.Should().Be(parent);
        hierarchyFromDatabase.Slug.Should().Be(slug);
    }
    
    [Fact]
    public async Task CreateManifest_CreatesManifest_WhenRewrittenPaths()
    {
        // Arrange
        var slug = TestIdentifiers.Id();
        var manifest = new PresentationManifest
        {
            Parent = $"http://no-customer.com/collections/{RootCollection.Id}",
            Slug = slug,
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "http://no-customer.com/canvases/someId"
                    }
                }
            ]
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        HttpRequestMessageBuilder.AddHostNoCustomerHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest.Id.Should().NotBeNull();
        responseManifest.Slug.Should().Be(slug);
        responseManifest.Parent.Should().Be($"http://no-customer.com/collections/{RootCollection.Id}");
        responseManifest.PaintedResources[0].CanvasPainting.CanvasId.Should().Be("http://no-customer.com/canvases/someId");
    }
    
    [Fact]
    public async Task CreateManifest_CreatesManifest_WhenRewrittenPathsAndAdditionalPathElements()
    {
        // Arrange
        var slug = TestIdentifiers.Id();
        var (_, canvasPaintingId) = TestIdentifiers.IdCanvasPainting();
        var manifest = new PresentationManifest
        {
            Parent = $"http://example.com/foo/1/collections/{RootCollection.Id}",
            Slug = slug,
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = $"http://example.com/example/1/canvases/{canvasPaintingId}"
                    },
                    Asset = new JObject
                    {
                        ["id"] = "someId",
                        ["mediaType"] = "image/jpeg"
                    }
                }
            ]
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        HttpRequestMessageBuilder.AddHostExampleHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest.Id.Should().NotBeNull();
        responseManifest.Slug.Should().Be(slug);
        responseManifest.Parent.Should().Be($"http://example.com/foo/1/collections/{RootCollection.Id}");
        responseManifest.PaintedResources[0].CanvasPainting.CanvasId.Should().Be($"http://example.com/example/1/canvases/{canvasPaintingId}");
        responseManifest.Items[0].Id.Should().Be($"https://localhost:7230/1/canvases/{canvasPaintingId}", "uses the settings based path parser");
    }
    
    [Fact]
    public async Task CreateManifest_CreatesManifest_WhenRewrittenPathsNotMatchingCallingApi()
    {
        // Arrange
        var slug = TestIdentifiers.Id();
        var manifest = new PresentationManifest
        {
            Parent = $"http://example.com/foo/1/collections/{RootCollection.Id}",
            Slug = slug,
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "http://example.com/example/1/canvases/someId"
                    },
                    Asset = new JObject
                    {
                        ["id"] = "someId",
                        ["mediaType"] = "image/jpeg"
                    }
                }
            ]
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest.Id.Should().NotBeNull();
        responseManifest.Slug.Should().Be(slug);
        responseManifest.Parent.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");
        responseManifest.PaintedResources[0].CanvasPainting.CanvasId.Should().Be("http://localhost/1/canvases/someId");
        responseManifest.Items[0].Id.Should().Be("https://localhost:7230/1/canvases/someId", "uses the settings based path parser");
    }

    [Fact]
    public async Task UpdateManifest_UpdatesManifest_WhenRewrittenPathsWithNoCustomer()
    {
        // Arrange
        var createdDate = DateTime.UtcNow.AddDays(-1);
        var dbManifest = (await dbContext.Manifests.AddTestManifest(createdDate: createdDate)).Entity;
        await dbContext.SaveChangesAsync();
        var parent = $"http://no-customer.com/collections/{RootCollection.Id}";
        var slug = $"changed_{dbManifest.Hierarchy.Single().Slug}";
        var manifest = dbManifest.ToPresentationManifest(parent: "root", slug: slug);
        manifest.Parent = parent;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        HttpRequestMessageBuilder.AddHostNoCustomerHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        responseManifest.Id.Should().NotBeNull();
        responseManifest.Slug.Should().Be(slug);
        responseManifest.Parent.Should().Be(parent);
    }
    
    [Fact]
    public async Task UpdateManifest_UpdatesManifest_WhenRewrittenPathsAndAdditionalPathElements()
    {
        // Arrange
        var createdDate = DateTime.UtcNow.AddDays(-1);
        var dbManifest = (await dbContext.Manifests.AddTestManifest(createdDate: createdDate)).Entity;
        await dbContext.SaveChangesAsync();
        var parent = $"http://example.com/foo/1/collections/{RootCollection.Id}";
        var slug = $"changed_{dbManifest.Hierarchy.Single().Slug}";
        var manifest = dbManifest.ToPresentationManifest(parent: "root", slug: slug);
        manifest.Parent = parent;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        HttpRequestMessageBuilder.AddHostExampleHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        responseManifest.Id.Should().NotBeNull();
        responseManifest.Slug.Should().Be(slug);
        responseManifest.Parent.Should().Be(parent);
    }
}
