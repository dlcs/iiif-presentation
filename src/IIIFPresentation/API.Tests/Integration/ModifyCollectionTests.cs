﻿#nullable disable

using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using API.Helpers;
using API.Infrastructure.IdGenerator;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using FakeItEasy;
using IIIF.Presentation.V3.Strings;
using Models.API.Collection;
using Models.API.Collection.Upsert;
using Models.API.General;
using Models.Database.Collections;
using Models.Database.General;
using Models.Infrastucture;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyCollectionTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    
    private readonly PresentationContext dbContext;
    
    private readonly IAmazonS3 amazonS3;

    private const int Customer = 1;

    private readonly string parent;
    
    public ModifyCollectionTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));

        parent = dbContext.Collections.First(x => x.CustomerId == Customer && x.Hierarchy!.Any(h => h.Slug == string.Empty)).Id;
        
        storageFixture.DbFixture.CleanUp();
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
            PresentationThumbnail = "some/thumbnail",
            Tags = "some, tags",
            ItemsOrder = 1,
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections", JsonSerializer.Serialize(collection));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        fromDatabase.Id.Length.Should().BeGreaterThan(6);
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection");
        hierarchyFromDatabase.Slug.Should().Be("programmatic-child");
        hierarchyFromDatabase.ItemsOrder.Should().Be(1);
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
    public async Task CreateCollection_CreatesCollection_WhenIsStorageCollectionFalse()
    {
        // Arrange
        var collection = $@"{{
   ""type"": ""Collection"",
   ""behavior"": [
       ""public-iiif""
   ],
   ""label"": {{
       ""en"": [
           ""iiif post""
       ]
   }},
    ""slug"": ""iiif-child"",
    ""parent"": ""{parent}"",
    ""tags"": ""some, tags"",
    ""itemsOrder"": 1,
    ""thumbnail"": [
        {{
          ""id"": ""https://example.org/img/thumb.jpg"",
          ""type"": ""Image"",
          ""format"": ""image/jpeg"",
          ""width"": 300,
          ""height"": 200
        }}
    ]
}}";

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections", collection);
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);
        
        var fromS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/collections/{fromDatabase.Id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("iiif post");
        hierarchyFromDatabase.Slug.Should().Be("iiif-child");
        hierarchyFromDatabase.ItemsOrder.Should().Be(1);
        fromDatabase.Tags.Should().Be("some, tags");
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeFalse();
        fromDatabase.Modified.Should().Be(fromDatabase.Created);
        fromDatabase.Thumbnail.Should().Be("https://example.org/img/thumb.jpg");
        responseCollection!.View!.PageSize.Should().Be(20);
        responseCollection.View.Page.Should().Be(1);
        responseCollection.View.Id.Should().Contain("?page=1&pageSize=20");
        fromS3.Should().NotBeNull();
    }
    
        [Fact]
    public async Task CreateCollection_ReturnsError_WhenIsStorageCollectionFalseAndUsingInvalidResource()
    {
        // Arrange
        var collection = new UpsertFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic
            },
            Label = new LanguageMap("en", ["test collection"]),
            Slug = "iiif-child",
            Parent = parent,
            Tags = "some, tags",
            PresentationThumbnail = "some/thumbnail",
            ItemsOrder = 1,
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections", JsonSerializer.Serialize(collection));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);
        
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be("An error occurred while attempting to validate the collection as IIIF");
    }
    
    [Fact]
    public async Task CreateCollection_FailsToCreateCollection_WhenDuplicateSlug()
    {
        // Arrange
        var collection = new PresentationCollection()
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
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/DuplicateSlugValue");
    }
    
    [Fact]
    public async Task CreateCollection_ReturnsUnauthorized_WhenCalledWithoutAuth()
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
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task CreateCollection_ReturnsForbidden_WhenCalledWithIncorrectShowExtraHeader()
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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
    public async Task GenerateUniqueIdAsync_CreatesNewId()
    {
        // Arrange
        var sqidsGenerator = A.Fake<IIdGenerator>();
        A.CallTo(() => sqidsGenerator.Generate(A<List<long>>.Ignored)).Returns("generated");
        

        // Act
        var id = await dbContext.Collections.GenerateUniqueIdAsync(1, sqidsGenerator);

        // Assert
        id.Should().Be("generated");
    }
    
    [Fact]
    public async Task GenerateUniqueIdAsync_ThrowsException_WhenGeneratingExistingId()
    {
        // Arrange
        var sqidsGenerator = A.Fake<IIdGenerator>();
        A.CallTo(() => sqidsGenerator.Generate(A<List<long>>.Ignored)).Returns("root");
        
        // Act
        Func<Task> action = () => dbContext.Collections.GenerateUniqueIdAsync(1, sqidsGenerator);

        // Assert
        await action.Should().ThrowAsync<ConstraintException>();
    }
    
    [Fact]
    public async Task UpdateCollection_ReturnsUnauthorized_WhenCalledWithoutAuth()
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
        
        var collectionName = nameof(UpdateCollection_ReturnsUnauthorized_WhenCalledWithoutAuth);

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{collectionName}", JsonSerializer.Serialize(collection));
        
        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task UpdateCollection_ReturnsForbidden_WhenCalledWithIncorrectShowExtraHeader()
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

        var collectionName = nameof(UpdateCollection_ReturnsForbidden_WhenCalledWithIncorrectShowExtraHeader);
        
        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"{Customer}/collections/{collectionName}");
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "Incorrect");
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
            CustomerId = 1
        };
        
        await dbContext.Hierarchy.AddAsync(new Hierarchy
        {
            CollectionId = "UpdateTester",
            Slug = "update-test",
            Parent = RootCollection.Id,
            Type = ResourceType.StorageCollection,
            CustomerId = 1,
            Canonical = true
        });
        
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
            PresentationThumbnail = "some/location/2",
            Tags = "some, tags, 2"
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", JsonSerializer.Serialize(updatedCollection));
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue(getResponse.Headers.ETag!.Tag));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(updateRequestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection - updated");
        hierarchyFromDatabase.Slug.Should().Be("programmatic-child");
        hierarchyFromDatabase.ItemsOrder.Should().Be(1);
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
            PresentationThumbnail = "some/location/2",
            Tags = "some, tags, 2",
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/createFromUpdate", JsonSerializer.Serialize(updatedCollection));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(updateRequestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fromDatabase.Id.Should().Be("createFromUpdate");
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection - create from update");
        hierarchyFromDatabase.Slug.Should().Be("create-from-update");
        hierarchyFromDatabase.ItemsOrder.Should().Be(1);
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
            PresentationThumbnail = "some/location/2",
            Tags = "some, tags, 2",
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/createFromUpdate2", JsonSerializer.Serialize(updatedCollection));
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue("\"someTag\""));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(updateRequestMessage);
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        error!.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ETagNotAllowed");
    }
    
    [Fact]
    public async Task UpdateCollection_UpdatesCollection_WhenAllValuesProvidedWithoutLabel()
    {
        // Arrange
        var initialCollection = new Collection()
        {
            Id = "UpdateTester-2",
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
            CustomerId = 1
        };
        
        await dbContext.Hierarchy.AddAsync(new Hierarchy
        {
            CollectionId = "UpdateTester-2",
            Slug = "update-test-2",
            Parent = RootCollection.Id,
            Type = ResourceType.StorageCollection,
            CustomerId = 1,
            Canonical = true
        });
        
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

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        hierarchyFromDatabase.Parent.Should().Be(parent);
        hierarchyFromDatabase.Slug.Should().Be("programmatic-child-2");
        fromDatabase.Label.Should().BeNull();
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeTrue();
    }
    
    [Fact (Skip = "Test to be updated to pass in https://github.com/dlcs/iiif-presentation/issues/27")]
    public async Task UpdateCollection_FailsToUpdateCollection_WhenNotStorageCollection()
    {
        // Arrange
        var initialCollection = new Collection()
        {
            Id = "UpdateTester-3",
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
            CustomerId = 1
        };
        
        await dbContext.Hierarchy.AddAsync(new Hierarchy
        {
            CollectionId = "UpdateTester-3",
            Slug = "update-test-3",
            Parent = RootCollection.Id,
            Type = ResourceType.StorageCollection,
            CustomerId = 1
        });
        
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
            CustomerId = 1
        };
        
        await dbContext.Hierarchy.AddAsync(new Hierarchy
        {
            CollectionId = "UpdateTester-4",
            Slug = "update-test-4",
            Parent = RootCollection.Id,
            Type = ResourceType.StorageCollection,
            CustomerId = 1,
            Canonical = true
        });
        
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
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be("The parent collection could not be found");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ParentCollectionNotFound");
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
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        error!.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ETagNotMatched");
    }
    
    [Fact]
    public async Task UpdateCollection_FailsToUpdateCollection_WhenChangingParentToChild()
    {
        var parentCollection = new Collection
        {
            Id = "UpdateTester-5",
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
            Hierarchy =
            [
                new()
                {
                    Canonical = true,
                    CollectionId = "UpdateTester-5",
                    Slug = "update-test-5",
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection,
                    CustomerId = 1
                }
            ]
        };
        
        var childCollection = new Collection
        {
            Id = "UpdateTester-6",
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
            Hierarchy =
            [
                new()
                {
                    Canonical = true,
                    CollectionId = "UpdateTester-6",
                    Slug = "update-test-6",
                    Parent = parentCollection.Id,
                    Type = ResourceType.StorageCollection,
                    CustomerId = 1
                }
            ]
        };
        
        await dbContext.Collections.AddAsync(parentCollection);
        await dbContext.Collections.AddAsync(childCollection);
        await dbContext.SaveChangesAsync();
        
        var updatedCollection = new UpsertFlatCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = parentCollection.Hierarchy.Single(h => h.Canonical).Slug,
            Parent = childCollection.Id
        };
        
        var getRequestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get,
                $"{Customer}/collections/{parentCollection.Id}");
        var getResponse = await httpClient.AsCustomer(1).SendAsync(getRequestMessage);

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"1/collections/{parentCollection.Id}", JsonSerializer.Serialize(updatedCollection));
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue(getResponse.Headers.ETag!.Tag));
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(updateRequestMessage);
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/PossibleCircularReference");
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
    public async Task DeleteCollection_ReturnsUnauthorized_WhenCalledWithoutAuth()
    {
        // Arrange
        var collectionName = nameof(DeleteCollection_ReturnsUnauthorized_WhenCalledWithoutAuth);

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete, $"{Customer}/collections/{collectionName}");
        
        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task DeleteCollection_ReturnsForbidden_WhenCalledWithIncorrectShowExtraHeader()
    {
        // Arrange
        var collectionName = nameof(DeleteCollection_ReturnsForbidden_WhenCalledWithIncorrectShowExtraHeader);
        
        var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{Customer}/collections/{collectionName}");
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "Incorrect");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
            CustomerId = 1
        };
        
        await dbContext.Hierarchy.AddAsync(new Hierarchy
        {
            CollectionId = "DeleteTester",
            Slug = "delete-test",
            Parent = RootCollection.Id,
            Type = ResourceType.StorageCollection,
            CustomerId = 1,
            Canonical = true
        });
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();

        var deleteRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/collections/{initialCollection.Id}");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(deleteRequestMessage);

        var fromDatabase = dbContext.Collections.FirstOrDefault(c => c.Id == initialCollection.Id);
        var fromDatabaseHierarchy = dbContext.Hierarchy.FirstOrDefault(c => c.CollectionId == initialCollection.Id);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        fromDatabase.Should().BeNull();
        fromDatabaseHierarchy.Should().BeNull();
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
    
    [Fact]
    public async Task CreateCollection_CreatesMinimalCollection_ViaHierarchicalCollection()
    {
        // Arrange
        var slug = "iiif-collection-post";
        
        var collection = @"{
   ""type"": ""Collection"",
   ""behavior"": [
       ""public-iiif""
   ],
   ""label"": {
       ""en"": [
           ""iiif hierarchical post""
       ]
   }
}";

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/{slug}", collection);
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);
        
        var responseCollection = await response.ReadAsIIIFJsonAsync<IIIF.Presentation.V3.Collection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Hierarchy!.Single(h => h.Canonical).Slug == slug);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.Slug == slug);
        
        var fromS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/collections/{fromDatabase.Id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        responseCollection!.Items.Should().BeNull();
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("iiif hierarchical post");
        hierarchyFromDatabase.Slug.Should().Be(slug);
        fromDatabase.Thumbnail.Should().BeNull();
        fromDatabase.Tags.Should().BeNull();
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeFalse();
        fromDatabase.Modified.Should().Be(fromDatabase.Created);
        fromS3.Should().NotBeNull();
    }
    
    [Fact]
    public async Task CreateCollection_CreatesCollectionWithThumbnailAndItems_ViaHierarchicalCollection()
    {
        // Arrange
        var slug = "iiif-collection-post-2";
        
        var collection = @"{
   ""type"": ""Collection"",
   ""behavior"": [
       ""public-iiif""
   ],
   ""label"": {
       ""en"": [
           ""iiif hierarchical post""
       ]
   },
    ""thumbnail"": [
        {
          ""id"": ""https://example.org/img/thumb.jpg"",
          ""type"": ""Image"",
          ""format"": ""image/jpeg"",
          ""width"": 300,
          ""height"": 200
        }
    ],
    ""items"": [
        {
        ""id"": ""https://some.id/iiif/collection"",
        ""type"": ""Collection"",
        }
    ]
}";

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/{slug}", collection);
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);
        
        var responseCollection = await response.ReadAsIIIFJsonAsync<IIIF.Presentation.V3.Collection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Hierarchy!.Single(h => h.Canonical).Slug == slug);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.Slug == id);
        
        var fromS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/collections/{fromDatabase.Id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        responseCollection.Items!.Count.Should().Be(1);
        responseCollection.Thumbnail.Should().NotBeNull();
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("iiif hierarchical post");
        hierarchyFromDatabase.Slug.Should().Be(slug);
        fromDatabase.Thumbnail.Should().Be("https://example.org/img/thumb.jpg");
        fromDatabase.Tags.Should().BeNull();
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeFalse();
        fromDatabase.Modified.Should().Be(fromDatabase.Created);
        fromS3.Should().NotBeNull();
    }
}