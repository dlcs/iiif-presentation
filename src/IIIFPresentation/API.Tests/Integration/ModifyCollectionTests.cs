using System.Net;
using System.Net.Http.Headers;
using System.Text;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using FluentAssertions;
using IIIF.Presentation.V3.Strings;
using Microsoft.AspNetCore.Mvc.Testing;
using Models.API.Collection;
using Models.API.Collection.Update;
using Models.API.General;
using Models.Database.Collections;
using Models.Infrastucture;
using Repository;
using Test.Helpers.Integration;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ModifyCollectionTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    
    private readonly PresentationContext dbContext;

    private const int Customer = 1;

    private readonly string parent;
    
    public ModifyCollectionTests(PresentationContextFixture dbFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = dbFixture.DbContext;
        
        httpClient = factory.WithConnectionString(dbFixture.ConnectionString)
            .CreateClient(new WebApplicationFactoryClientOptions());

        parent = dbContext.Collections.First(x => x.CustomerId == Customer && x.Slug == string.Empty).Id;
        
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task CreateCollection_CreatesCollection_WhenAllValuesProvided()
    {
        // Arrange
        var collection = new FlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection"]),
            Slug = "programmatic-child",
            Parent = parent
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections", JsonSerializer.Serialize(collection));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<FlatCollection>();

        var fromDatabase = dbContext.Collections.First(c => c.Id == responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        fromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection");
        fromDatabase.Slug.Should().Be("programmatic-child");
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeTrue();
    }
    
    [Fact]
    public async Task CreateCollection_FailsToCreateCollection_WhenDuplicateSlug()
    {
        // Arrange
        var collection = new FlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection"]),
            Slug = "first-child",
            Parent = parent
        };
        
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections", JsonSerializer.Serialize(collection));

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);
        
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict); 
        error!.Detail.Should().Be("The collection could not be created due to a duplicate slug value");
    }
    
    [Fact]
    public async Task CreateCollection_FailsToCreateCollection_WhenCalledWithoutAuth()
    {
        // Arrange
        var collection = new FlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection"]),
            Slug = "first-child",
            Parent = parent
        };
        
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections", JsonSerializer.Serialize(collection));
        
        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    
    [Fact]
    public async Task CreateCollection_FailsToCreateCollection_WhenCalledWithIncorrectShowExtraHeader()
    {
        // Arrange
        var collection = new FlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection"]),
            Slug = "first-child",
            Parent = parent
        };
        
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{Customer}/collections");
        requestMessage.Headers.Add("IIIF-CS-Show-Extras", "Incorrect");
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(collection), Encoding.UTF8,
            new MediaTypeHeaderValue("application/json"));
        
        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    
    [Fact]
    public async Task CreateCollection_FailsToCreateCollection_WhenCalledWithoutShowExtras()
    {
        // Arrange
        var collection = new FlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection"]),
            Slug = "first-child",
            Parent = parent
        };
        
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{Customer}/collections");
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(collection), Encoding.UTF8,
            new MediaTypeHeaderValue("application/json"));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    
    [Fact]
    public async Task UpdateCollection_UpdatesCollection_WhenAllValuesProvided()
    {
        // Arrange
        var initialCollection = new Collection()
        {
            Id = "UpdateTester",
            Slug = "update-test",
            UsePath = true,
            Label = new LanguageMap
            {
                { "en", new List<string> { "update testing" } }
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = true,
            IsPublic = false,
            CustomerId = 1,
            Parent = "RootStorage"
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        // TODO: remove this when better ETag support is implemented - this implementation requires GET to be called to retrieve the ETag
        var getRequestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{Customer}/collections/{initialCollection.Id}");
        
        var getResponse = await httpClient.AsCustomer(1).SendAsync(getRequestMessage);
        
        var updatedCollection = new UpdateFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = "programmatic-child",
            Parent = parent
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", JsonSerializer.Serialize(updatedCollection));
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue(getResponse.Headers.ETag!.Tag));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(updateRequestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<FlatCollection>();

        var fromDatabase = dbContext.Collections.First(c => c.Id == responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection - updated");
        fromDatabase.Slug.Should().Be("programmatic-child");
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeTrue();
    }
    
    [Fact]
    public async Task UpdateCollection_UpdatesCollection_WhenAllValuesProvidedWithoutLabel()
    {
        // Arrange
        var initialCollection = new Collection()
        {
            Id = "UpdateTester-2",
            Slug = "update-test-2",
            UsePath = true,
            Label = new LanguageMap
            {
                { "en", new List<string> { "update testing" } }
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = true,
            IsPublic = false,
            CustomerId = 1,
            Parent = "RootStorage"
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        // TODO: remove this when better ETag support is implemented - this implementation requires GET to be called to retrieve the ETag
        var getRequestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{Customer}/collections/{initialCollection.Id}");
        
        var getResponse = await httpClient.AsCustomer(1).SendAsync(getRequestMessage);
        
        var updatedCollection = new UpdateFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Slug = "programmatic-child-2",
            Parent = parent
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", JsonSerializer.Serialize(updatedCollection));
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue(getResponse.Headers.ETag!.Tag));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(updateRequestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<FlatCollection>();

        var fromDatabase = dbContext.Collections.First(c => c.Id == responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fromDatabase.Parent.Should().Be(parent);
        fromDatabase.Slug.Should().Be("programmatic-child-2");
        fromDatabase.Label.Should().BeNull();
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeTrue();
    }
    
    [Fact]
    public async Task UpdateCollection_FailsToUpdateCollection_WhenNotStorageCollection()
    {
        // Arrange
        var initialCollection = new Collection()
        {
            Id = "UpdateTester-3",
            Slug = "update-test-3",
            UsePath = true,
            Label = new LanguageMap
            {
                { "en", new List<string> { "update testing" } }
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = true,
            IsPublic = false,
            CustomerId = 1,
            Parent = "RootStorage"
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        // TODO: remove this when better ETag support is implemented - this implementation requires GET to be called to retrieve the ETag
        var getRequestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{Customer}/collections/{initialCollection.Id}");
        
        var getResponse = await httpClient.AsCustomer(1).SendAsync(getRequestMessage);
        
        var updatedCollection = new UpdateFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic
            },
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = "programmatic-child-3",
            Parent = parent
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", JsonSerializer.Serialize(updatedCollection));
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue(getResponse.Headers.ETag!.Tag));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(updateRequestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task UpdateCollection_FailsToUpdateCollection_WhenETagIncorrect()
    {
        var updatedCollection = new UpdateFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = "programmatic-child",
            Parent = parent
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            "1/collections/FirstChildCollection", JsonSerializer.Serialize(updatedCollection));
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue("\"notReal\""));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(updateRequestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }
    
    [Fact]
    public async Task UpdateCollection_FailsToUpdateCollection_WhenCalledWithoutNeededHeaders()
    {
        // Arrange
        var getRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/collections/FirstChildCollection");
        
        var getResponse = await httpClient.AsCustomer(1).SendAsync(getRequestMessage);
        
        var updatedCollection = new UpdateFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = "programmatic-child",
            Parent = parent
        };
        
        var updateRequestMessage = new HttpRequestMessage(HttpMethod.Put, "1/collections/FirstChildCollection");
        updateRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer");
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue(getResponse.Headers.ETag!.Tag));
        updateRequestMessage.Content = new StringContent(JsonSerializer.Serialize(updatedCollection), Encoding.UTF8,
            new MediaTypeHeaderValue("application/json"));
        
        // Act
        var response = await httpClient.SendAsync(updateRequestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}