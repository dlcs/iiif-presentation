using System.Net;
using System.Net.Http.Headers;
using System.Text;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using FluentAssertions;
using IIIF.Presentation.V3.Strings;
using Microsoft.AspNetCore.Mvc.Testing;
using Models.API.Collection;
using Models.API.Collection.Upsert;
using Models.API.General;
using Models.Database.Collections;
using Models.Infrastucture;
using Repository;
using Test.Helpers.Helpers;
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
        var collection = new UpsertFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection"]),
            Slug = "programmatic-child",
            Parent = parent,
            Thumbnail = "some/thumbnail",
            Tags = "some, tags",
            ItemsOrder = 1,
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
        fromDatabase.ItemsOrder.Should().Be(1);
        fromDatabase.Thumbnail.Should().Be("some/thumbnail");
        fromDatabase.Tags.Should().Be("some, tags");
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeTrue();
        fromDatabase.Modified.Should().Be(fromDatabase.Created);
        responseCollection!.View!.PageSize.Should().Be(20);
        responseCollection.View.Page.Should().Be(1);
        responseCollection.View.Id.Should().Contain("?page=1&pageSize=20");
    }
    
    [Fact]
    public async Task CreateCollection_FailsToCreateCollection_WhenIsStorageCollectionFalse()
    {
        // Arrange
        var collection = new UpsertFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic
            },
            Label = new LanguageMap("en", ["test collection"]),
            Slug = "programmatic-child",
            Parent = parent
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections", JsonSerializer.Serialize(collection));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
        var collection = new UpsertFlatCollection()
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
        var collection = new UpsertFlatCollection()
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
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "Incorrect");
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
        var collection = new UpsertFlatCollection()
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
            Parent = "root"
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        // TODO: remove this when better ETag support is implemented - this implementation requires GET to be called to retrieve the ETag
        var getRequestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{Customer}/collections/{initialCollection.Id}");
        
        var getResponse = await httpClient.AsCustomer(1).SendAsync(getRequestMessage);
        
        var updatedCollection = new UpsertFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = "programmatic-child",
            Parent = parent,
            ItemsOrder = 1,
            Thumbnail = "some/location/2",
            Tags = "some, tags, 2",
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
        fromDatabase.ItemsOrder.Should().Be(1);
        fromDatabase.Thumbnail.Should().Be("some/location/2");
        fromDatabase.Tags.Should().Be("some, tags, 2");
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeTrue();
        responseCollection!.View!.PageSize.Should().Be(20);
        responseCollection.View.Page.Should().Be(1);
        responseCollection.View.Id.Should().Contain("?page=1&pageSize=20");
    }
    
    [Fact]
    public async Task UpdateCollection_CreatesCollection_WhenUnknownCollectionIdProvided()
    {
        // Arrange
        var updatedCollection = new UpsertFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection - create from update"]),
            Slug = "create-from-update",
            Parent = parent,
            ItemsOrder = 1,
            Thumbnail = "some/location/2",
            Tags = "some, tags, 2",
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/createFromUpdate", JsonSerializer.Serialize(updatedCollection));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(updateRequestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<FlatCollection>();

        var fromDatabase = dbContext.Collections.First(c => c.Id == responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fromDatabase.Id.Should().Be("createFromUpdate");
        fromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection - create from update");
        fromDatabase.Slug.Should().Be("create-from-update");
        fromDatabase.ItemsOrder.Should().Be(1);
        fromDatabase.Thumbnail.Should().Be("some/location/2");
        fromDatabase.Tags.Should().Be("some, tags, 2");
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeTrue();
        responseCollection!.View!.PageSize.Should().Be(20);
        responseCollection.View.Page.Should().Be(1);
        responseCollection.View.Id.Should().Contain("?page=1&pageSize=20");
    }
    
    [Fact]
    public async Task UpdateCollection_FailsToCreateCollection_WhenUnknownCollectionWithETag()
    {
        // Arrange
        var updatedCollection = new UpsertFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection - create from update"]),
            Slug = "create-from-update-2",
            Parent = parent,
            ItemsOrder = 1,
            Thumbnail = "some/location/2",
            Tags = "some, tags, 2",
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/createFromUpdate2", JsonSerializer.Serialize(updatedCollection));
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue("\"someTag\""));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(updateRequestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
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
            Parent = "root"
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        // TODO: remove this when better ETag support is implemented - this implementation requires GET to be called to retrieve the ETag
        var getRequestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{Customer}/collections/{initialCollection.Id}");
        
        var getResponse = await httpClient.AsCustomer(1).SendAsync(getRequestMessage);
        
        var updatedCollection = new UpsertFlatCollection()
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
            Parent = "root"
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        // TODO: remove this when better ETag support is implemented - this implementation requires GET to be called to retrieve the ETag
        var getRequestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{Customer}/collections/{initialCollection.Id}");
        
        var getResponse = await httpClient.AsCustomer(1).SendAsync(getRequestMessage);
        
        var updatedCollection = new UpsertFlatCollection()
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
    public async Task UpdateCollection_FailsToUpdateCollection_WhenParentDoesNotExist()
    {
        // Arrange
        var initialCollection = new Collection()
        {
            Id = "UpdateTester-4",
            Slug = "update-test-4",
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
            Parent = "root"
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        // TODO: remove this when better ETag support is implemented - this implementation requires GET to be called to retrieve the ETag
        var getRequestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{Customer}/collections/{initialCollection.Id}");
        
        var getResponse = await httpClient.AsCustomer(1).SendAsync(getRequestMessage);
        
        var updatedCollection = new UpsertFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = "programmatic-child-3",
            Parent = "doesNotExist"
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", JsonSerializer.Serialize(updatedCollection));
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue(getResponse.Headers.ETag!.Tag));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(updateRequestMessage);
        var responseCollection = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        responseCollection!.Detail.Should().Be("The parent collection could not be found");
    }
    
    [Fact]
    public async Task UpdateCollection_FailsToUpdateCollection_WhenETagIncorrect()
    {
        var updatedCollection = new UpsertFlatCollection()
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
        
        var updatedCollection = new UpsertFlatCollection()
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
    
    [Fact]
    public async Task DeleteCollection_DeletesCollection_WhenAllValuesProvided()
    {
        // Arrange
        var initialCollection = new Collection()
        {
            Id = "DeleteTester",
            Slug = "delete-test",
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
            Parent = "root"
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        

        var deleteRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/collections/{initialCollection.Id}");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(deleteRequestMessage);

        var fromDatabase = dbContext.Collections.FirstOrDefault(c => c.Id == initialCollection.Id);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        fromDatabase.Should().BeNull();
    }
    
    [Fact]
    public async Task DeleteCollection_FailsToDeleteCollection_WhenNotFound()
    {
        // Arrange
        var deleteRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/collections/doesNotExist");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(deleteRequestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task DeleteCollection_FailsToDeleteCollection_WhenAttemptingToDeleteRoot()
    {
        // Arrange
        var deleteRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/collections/{RootCollection.Id}");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(deleteRequestMessage);

        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        errorResponse!.ErrorTypeUri.Should().Be("http://localhost/errors/DeleteCollectionType/CannotDeleteRootCollection");
        errorResponse.Detail.Should().Be("Cannot delete a root collection");
    }
    
    
    [Fact]
    public async Task DeleteCollection_FailsToDeleteCollection_WhenAttemptingToDeleteRootDirectly()
    {
        // Arrange
        var deleteRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/collections/{RootCollection.Id}");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(deleteRequestMessage);

        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        errorResponse!.ErrorTypeUri.Should().Be("http://localhost/errors/DeleteCollectionType/CannotDeleteRootCollection");
        errorResponse.Detail.Should().Be("Cannot delete a root collection");
    }
    
    [Fact]
    public async Task DeleteCollection_FailsToDeleteCollection_WhenAttemptingToDeleteCollectionWithItems()
    {
        // Arrange
        var deleteRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/collections/FirstChildCollection");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(deleteRequestMessage);

        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        errorResponse!.ErrorTypeUri.Should().Be("http://localhost/errors/DeleteCollectionType/CollectionNotEmpty");
        errorResponse.Detail.Should().Be("Cannot delete a collection with child items");
    }
}