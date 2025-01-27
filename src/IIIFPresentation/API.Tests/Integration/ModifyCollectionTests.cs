#nullable disable

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Amazon.S3;
using API.Infrastructure.Helpers;
using API.Infrastructure.Validation;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.API.Collection;
using Models.API.General;
using Models.Database.Collections;
using Models.Database.General;
using Models.Infrastucture;
using Newtonsoft.Json.Linq;
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
    private readonly IETagManager etagManager;
    private const int Customer = 1;
    private readonly string parent;

    public ModifyCollectionTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));
        
        etagManager = (IETagManager)factory.Services.GetRequiredService(typeof(IETagManager));

        parent = dbContext.Collections
            .First(x => x.CustomerId == Customer && x.Hierarchy!.Any(h => h.Slug == string.Empty)).Id;

        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task CreateCollection_CreatesCollection_Location_Returns_Created()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/collections",
            TestContent.Bug_158.Collection);
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        response.Headers.Location.Should().NotBeNull();
        var getUrl = response.Headers.Location!.ToString();

        requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, getUrl);

        // Act
        response = await httpClient.AsCustomer().SendAsync(requestMessage);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        responseCollection.Should().NotBeNull();
        responseCollection!.Slug.Should().Be(TestContent.Bug_158.CollectionName);
    }
    
    [Fact]
    public async Task CreateCollection_CreatesCollection_WhenAllValuesProvided()
    {
        var slug = nameof(CreateCollection_CreatesCollection_WhenAllValuesProvided);
        // Arrange
        var collection = new PresentationCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection"]),
            Slug = slug,
            Parent = parent,
            PresentationThumbnail = "some/thumbnail",
            Tags = "some, tags",
            ItemsOrder = 1,
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            JsonSerializer.Serialize(collection));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        fromDatabase.Id.Length.Should().BeGreaterThan(6);
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection");
        hierarchyFromDatabase.Slug.Should().Be(slug);
        hierarchyFromDatabase.ItemsOrder.Should().Be(1);
        fromDatabase.Thumbnail.Should().Be("some/thumbnail");
        fromDatabase.Tags.Should().Be("some, tags");
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeTrue();
        fromDatabase.Modified.Should().Be(fromDatabase.Created);
        responseCollection!.View!.PageSize.Should().Be(20);
        responseCollection.View.Page.Should().Be(1);
        responseCollection.View.Id.Should().Contain("?page=1&pageSize=20");
        responseCollection.PartOf.Single().Id.Should().Be("http://localhost/1/collections/root");
        responseCollection.PartOf.Single().Label["en"].Single().Should().Be("repository root");
        responseCollection.Totals.Should().BeEquivalentTo(DescendantCounts.Empty, "Storage collections have empty counts");
        
        var context = (JArray)responseCollection.Context;
        context.First.Value<string>().Should().Be("http://tbc.org/iiif-repository/1/context.json");
        context.Last.Value<string>().Should().Be("http://iiif.io/api/presentation/3/context.json");
        
    }

    public static TheoryData<string> ProhibitedSlugProvider =>
        new(SpecConstants.ProhibitedSlugs);

    [Theory]
    [MemberData(nameof(ProhibitedSlugProvider))]
    public async Task CreateCollection_DoesntCreatesCollection_WhenProhibitedSlug(string slug)
    {
        // Arrange
        var collection = new PresentationCollection
        {
            Behavior = new()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new("en", ["test collection"]),
            Slug = slug,
            Parent = parent,
            PresentationThumbnail = "some/thumbnail",
            Tags = "some, tags",
            ItemsOrder = 1
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            JsonSerializer.Serialize(collection));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be($"'slug' cannot be one of prohibited terms: '{slug}'");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ValidationFailed");
    }
    
    [Fact]
    public async Task CreateCollection_CreatesIIIFCollection_WhenAllValuesProvided()
    {
        // Arrange
        var slug = nameof(CreateCollection_CreatesIIIFCollection_WhenAllValuesProvided);
        var collection = new PresentationCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
            },
            Label = new LanguageMap("en", ["test collection"]),
            Slug = slug,
            Parent = parent,
            PresentationThumbnail = "some/thumbnail",
            Tags = "some, tags",
            ItemsOrder = 1,
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        fromDatabase.Id.Length.Should().BeGreaterThan(6);
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection");
        hierarchyFromDatabase.Slug.Should().Be(slug);
        hierarchyFromDatabase.ItemsOrder.Should().Be(1);
        fromDatabase.Thumbnail.Should().Be("some/thumbnail");
        fromDatabase.Tags.Should().Be("some, tags");
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeFalse();
        fromDatabase.Modified.Should().Be(fromDatabase.Created);
        responseCollection!.View!.PageSize.Should().Be(20);
        responseCollection.View.Page.Should().Be(1);
        responseCollection.View.Id.Should().Contain("?page=1&pageSize=20");
        responseCollection.PartOf.Single().Id.Should().Be("http://localhost/1/collections/root");
        responseCollection.PartOf.Single().Label["en"].Single().Should().Be("repository root");
        responseCollection.Totals.Should().BeNull("IIIF collections have no totals");
        
        var context = (JArray)responseCollection.Context;
        context.First.Value<string>().Should().Be("http://tbc.org/iiif-repository/1/context.json");
        context.Last.Value<string>().Should().Be("http://iiif.io/api/presentation/3/context.json");
    }
    
    [Fact]
    public async Task CreateCollection_CreatesIIIFCollection_AndSetsCorrectContext_RegardlessOfPassedContext()
    {
        // Arrange
        var slug = nameof(CreateCollection_CreatesIIIFCollection_AndSetsCorrectContext_RegardlessOfPassedContext);
        var collection = new PresentationCollection()
        {
            Context = "foo",
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
            },
            Label = new LanguageMap("en", ["test collection"]),
            Slug = slug,
            Parent = parent,
            PresentationThumbnail = "some/thumbnail",
            Tags = "some, tags",
            ItemsOrder = 1,
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        fromDatabase.Id.Length.Should().BeGreaterThan(6);
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection");
        hierarchyFromDatabase.Slug.Should().Be(slug);
        hierarchyFromDatabase.ItemsOrder.Should().Be(1);
        fromDatabase.Thumbnail.Should().Be("some/thumbnail");
        fromDatabase.Tags.Should().Be("some, tags");
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeFalse();
        fromDatabase.Modified.Should().Be(fromDatabase.Created);
        responseCollection!.View!.PageSize.Should().Be(20);
        responseCollection.View.Page.Should().Be(1);
        responseCollection.View.Id.Should().Contain("?page=1&pageSize=20");
        responseCollection.Totals.Should().BeNull("IIIF collections have no totals");
        
        var context = (JArray)responseCollection.Context;
        context.First.Value<string>().Should().Be("http://tbc.org/iiif-repository/1/context.json");
        context.Last.Value<string>().Should().Be("http://iiif.io/api/presentation/3/context.json");
    }

    [Fact]
    public async Task CreateCollection_CreatesCollection_WhenAllValuesProvidedAndParentIsFullUri()
    {
        // Arrange
        var collection = new PresentationCollection
        {
            Behavior = new()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new("en", ["test collection"]),
            Slug = "programmatic-child",
            Parent = $"http://localhost/1/collections/{parent}",
            PresentationThumbnail = "some/thumbnail",
            Tags = "some, tags",
            ItemsOrder = 1
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            JsonSerializer.Serialize(collection));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
        responseCollection.PartOf.Single().Id.Should().Be("http://localhost/1/collections/root");
        responseCollection.PartOf.Single().Label["en"].Single().Should().Be("repository root");
        responseCollection.Totals.Should().BeEquivalentTo(DescendantCounts.Empty);
    }

    [Fact]
    public async Task CreateCollection_FailsToCreateCollection_WhenParentIsHierarchicalUri()
    {
        // Arrange
        var collection = new PresentationCollection
        {
            Behavior = new()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new("en", ["test collection"]),
            Slug = "programmatic-child",
            Parent = $"http://localhost/1/{parent}", //note how this is HIERARCHICAL uri
            PresentationThumbnail = "some/thumbnail",
            Tags = "some, tags",
            ItemsOrder = 1
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be("The parent collection could not be found");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ParentCollectionNotFound");
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
    ],
""homepage"": [
  {{
    ""id"": ""https://www.getty.edu/art/collection/object/103RQQ"",
    ""type"": ""Text"",
    ""label"": {{
      ""en"": [
        ""Home page at the Getty Museum Collection""
      ]
    }},
    ""format"": ""text/html"",
    ""language"": [
      ""en""
    ]
  }}
]
}}";

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections", collection);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
        responseCollection.Homepage.Should().NotBeNull();
        responseCollection.PartOf.Single().Id.Should().Be("http://localhost/1/collections/root");
        responseCollection.PartOf.Single().Label["en"].Single().Should().Be("repository root");
        fromS3.Should().NotBeNull();
        responseCollection.Totals.Should().BeNull();
    }
    
    [Fact]
    public async Task CreateCollection_ReturnsError_WhenIsStorageCollectionFalseAndUsingInvalidResource()
    {
        // Arrange
        var collection = new PresentationCollection()
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

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            JsonSerializer.Serialize(collection));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be("An error occurred while attempting to validate the collection as IIIF");
    }
    
    [Fact]
    public async Task CreateCollection_ReturnsError_WhenIsStorageCollectionFalseAndUsingInvalidJson()
    {
        // Arrange
        var collection =
"""
{
   "behavior": [
     "public-iiif"
   ],
   "type": "Collection",
   "label": {
     "en": [
       "test collection - created"
     ]
   },
   "slug": "iiif-child",
   "parent": "root",
  "thumbnail": [
     {
       "id": "https://iiif.io/api/image/3.0/example/reference/someRef",
       "type": "Image",
       "format": "image/jpeg",
       "height": 100,
       "width": 100,
     }
   ],
   "homepage": "invalidHomepage"
   "itemsOrder": 1
}
""";

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections", 
            collection);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be("Could not deserialize collection");
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

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        error!.Detail.Should().Be("The collection could not be created due to a duplicate slug value");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/DuplicateSlugValue");
    }

    [Fact]
    public async Task CreateCollection_FailsToCreateCollection_WhenSlugOnlyDifferentInCasing()
    {
        // Arrange
        var collection = new PresentationCollection
        {
            Behavior = new()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new("en", ["test collection"]),
            Slug = "fIrSt-cHiLd",
            Parent = parent
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCollection_ReturnsForbidden_WhenCalledWithIncorrectShowExtraHeader()
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

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{Customer}/collections");
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "Incorrect");
        requestMessage.Content = new StringContent(collection.AsJson(), Encoding.UTF8,
            new MediaTypeHeaderValue("application/json"));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateCollection_FailsToCreateCollection_WhenCalledWithoutShowExtras()
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

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{Customer}/collections");
        requestMessage.Content = new StringContent(collection.AsJson(), Encoding.UTF8,
            new MediaTypeHeaderValue("application/json"));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    
    [Fact]
    public async Task CreateCollection_CreatesNonPublicIIIFCollection_WhenNullBehavior()
    {
        // Arrange
        var slug = nameof(CreateCollection_CreatesNonPublicIIIFCollection_WhenNullBehavior);
        var collection = $@"{{
   ""type"": ""Collection"",
   ""label"": {{
       ""en"": [
           ""iiif post""
       ]
   }},
    ""slug"": ""{slug}"",
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
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        fromDatabase.Id.Length.Should().BeGreaterThan(6);
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("iiif post");
        hierarchyFromDatabase.Slug.Should().Be(slug);
        hierarchyFromDatabase.ItemsOrder.Should().Be(1);
        fromDatabase.Thumbnail.Should().Be("https://example.org/img/thumb.jpg");
        fromDatabase.IsPublic.Should().BeFalse();
        fromDatabase.IsStorageCollection.Should().BeFalse();
        fromDatabase.Modified.Should().Be(fromDatabase.Created);
        responseCollection!.View!.PageSize.Should().Be(20);
        responseCollection.View.Page.Should().Be(1);
        responseCollection.View.Id.Should().Contain("?page=1&pageSize=20");
        responseCollection.PartOf.Single().Id.Should().Be("http://localhost/1/collections/root");
        responseCollection.PartOf.Single().Label["en"].Single().Should().Be("repository root");
        responseCollection.Totals.Should().BeNull();
        
        var context = (JArray)responseCollection.Context;
        context.First.Value<string>().Should().Be("http://tbc.org/iiif-repository/1/context.json");
        context.Last.Value<string>().Should().Be("http://iiif.io/api/presentation/3/context.json");
        
    }

    [Fact]
    public async Task UpdateCollection_ReturnsUnauthorized_WhenCalledWithoutAuth()
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

        var collectionName = nameof(UpdateCollection_ReturnsUnauthorized_WhenCalledWithoutAuth);

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{collectionName}", collection.AsJson());

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateCollection_ReturnsForbidden_WhenCalledWithIncorrectShowExtraHeader()
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

        var collectionName = nameof(UpdateCollection_ReturnsForbidden_WhenCalledWithIncorrectShowExtraHeader);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"{Customer}/collections/{collectionName}");
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "Incorrect");
        requestMessage.Content = new StringContent(collection.AsJson(), Encoding.UTF8,
            new MediaTypeHeaderValue("application/json"));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

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
                {"en", new() {"update testing"}}
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

        var slug = nameof(UpdateCollection_UpdatesCollection_WhenAllValuesProvided);
        await dbContext.Hierarchy.AddAsync(new Hierarchy
        {
            CollectionId = "UpdateTester",
            Slug = slug,
            Parent = RootCollection.Id,
            Type = ResourceType.StorageCollection,
            CustomerId = 1,
            Canonical = true
        });

        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        var updatedCollection = new PresentationCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = slug,
            Parent = parent,
            ItemsOrder = 1,
            PresentationThumbnail = "some/location/2",
            Tags = "some, tags, 2"
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", updatedCollection.AsJson());
        SetCorrectEtag(updateRequestMessage, initialCollection);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection - updated");
        hierarchyFromDatabase.Slug.Should().Be(slug);
        hierarchyFromDatabase.ItemsOrder.Should().Be(1);
        fromDatabase.Thumbnail.Should().Be("some/location/2");
        fromDatabase.Tags.Should().Be("some, tags, 2");
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeTrue();
        responseCollection!.View!.PageSize.Should().Be(20);
        responseCollection.View.Page.Should().Be(1);
        responseCollection.View.Id.Should().Contain("?page=1&pageSize=20");
        responseCollection.PartOf.Single().Id.Should().Be("http://localhost/1/collections/root");
        responseCollection.PartOf.Single().Label["en"].Single().Should().Be("repository root");
        responseCollection.Totals.Should().BeEquivalentTo(DescendantCounts.Empty);
        
        var context = (JArray)responseCollection.Context;
        context.First.Value<string>().Should().Be("http://tbc.org/iiif-repository/1/context.json");
        context.Last.Value<string>().Should().Be("http://iiif.io/api/presentation/3/context.json");
    }

    [Fact]
    public async Task UpdateCollection_UpdatesCollection_WhenAllValuesProvidedAndParentIsFullUri()
    {
        // Arrange
        var initialCollection = new Collection
        {
            Id = "FullUriUpdateTester",
            UsePath = true,
            Label = new()
            {
                {"en", new() {"update testing"}}
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

        await dbContext.Hierarchy.AddAsync(new()
        {
            CollectionId = "FullUriUpdateTester",
            Slug = "update-test",
            Parent = RootCollection.Id,
            Type = ResourceType.StorageCollection,
            CustomerId = 1,
            Canonical = true
        });

        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();

        var slug = nameof(UpdateCollection_UpdatesCollection_WhenAllValuesProvidedAndParentIsFullUri);
        var updatedCollection = new PresentationCollection
        {
            Behavior = new()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new("en", ["test collection - updated"]),
            Slug = slug,
            Parent = $"http://localhost/1/collections/{parent}",
            ItemsOrder = 1,
            PresentationThumbnail = "some/location/2",
            Tags = "some, tags, 2"
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", JsonSerializer.Serialize(updatedCollection));
        SetCorrectEtag(updateRequestMessage, initialCollection);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection - updated");
        hierarchyFromDatabase.Slug.Should().Be(slug);
        hierarchyFromDatabase.ItemsOrder.Should().Be(1);
        fromDatabase.Thumbnail.Should().Be("some/location/2");
        fromDatabase.Tags.Should().Be("some, tags, 2");
        fromDatabase.IsPublic.Should().BeTrue();
        fromDatabase.IsStorageCollection.Should().BeTrue();
        responseCollection!.View!.PageSize.Should().Be(20);
        responseCollection.View.Page.Should().Be(1);
        responseCollection.View.Id.Should().Contain("?page=1&pageSize=20");
        responseCollection.PartOf.Single().Id.Should().Be("http://localhost/1/collections/root");
        responseCollection.PartOf.Single().Label["en"].Single().Should().Be("repository root");
        responseCollection.Totals.Should().BeEquivalentTo(DescendantCounts.Empty);
    }
    
    [Fact]
    public async Task UpdateCollection_UpdatesIiifCollection_WhenAllValuesProvided()
    {
        // Arrange
        var initialCollection = new Collection()
        {
            Id = "UpdateTester-IIIF",
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
            IsStorageCollection = false,
            IsPublic = false,
            CustomerId = 1,
            Hierarchy = [
                new Hierarchy
                {
                    Slug = "iiif-update-test",
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection
                }
            ]
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();

        var updatedCollection = 
"""
{
  "behavior": [
    "public-iiif"
  ],
  "type": "Collection",
  "label": {
    "en": [
      "test collection - updated"
    ]
  },
  "slug": "iiif-programmatic-child",
  "parent": "root",
  "tags": "some, tags, 2",
 "thumbnail": [
    {
      "id": "https://iiif.io/api/image/3.0/example/reference/someRef",
      "type": "Image",
      "format": "image/jpeg",
      "height": 100,
      "width": 100,
    }
  ],
  "homepage": [
  {
    "id": "https://www.getty.edu/art/collection/object/103RQQ",
    "type": "Text",
    "label": {
      "en": [
        "Home page at the Getty Museum Collection"
      ]
    },
    "format": "text/html",
    "language": [
      "en"
    ]
  }
],
"metadata": [
  {
    "label": {
      "en": [
        "Artist"
      ]
    },
    "value": {
      "en": [
        "Winslow Homer (1836-1910)"
      ]
    }
  }
],
  "itemsOrder": 1
}
""";
        

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", updatedCollection);
        SetCorrectEtag(updateRequestMessage, initialCollection);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);
        
        var collection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();
        var fromDatabase = dbContext.Collections.Include(c => c.Hierarchy).First(c => c.Id == initialCollection.Id);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        collection.Metadata.Should().NotBeNull();
        collection.Homepage.Should().NotBeNull();
        collection.Thumbnail.Should().NotBeNull();
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection - updated"); 
        fromDatabase.Thumbnail.Should().Be("https://iiif.io/api/image/3.0/example/reference/someRef");
        fromDatabase.Hierarchy![0].Slug.Should().Be("iiif-programmatic-child");
        collection.Totals.Should().BeNull();
        
        var context = (JArray)collection.Context;
        context.First.Value<string>().Should().Be("http://tbc.org/iiif-repository/1/context.json");
        context.Last.Value<string>().Should().Be("http://iiif.io/api/presentation/3/context.json");
    }
    
    [Fact]
    public async Task UpdateCollection_FailsToUpdateCollection_WhenMovingIIIFCollectionToStorage()
    {
        // Arrange
        var initialCollection = new Collection()
        {
            Id = "IIIF-UpdateTester-2",
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
            IsStorageCollection = false,
            IsPublic = false,
            CustomerId = 1,
            Hierarchy = [
                new Hierarchy
                {
                    Slug = "update-test",
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection
                }
            ]
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        var updatedCollection = new PresentationCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = "programmatic-child",
            Parent = RootCollection.Id,
            ItemsOrder = 1,
            PresentationThumbnail = "some/location/2",
            Tags = "some, tags, 2"
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", updatedCollection.AsJson());
        SetCorrectEtag(updateRequestMessage, initialCollection);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        responseCollection.Detail.Should().Be("Cannot convert a Storage collection to a IIIF collection");
        responseCollection.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/CannotChangeCollectionType");
    }
    
    [Fact]
    public async Task UpdateCollection_FailsToUpdateCollection_WhenMovingStorageCollectionToIIIF()
    {
        var id = nameof(UpdateCollection_FailsToUpdateCollection_WhenMovingStorageCollectionToIIIF);
        var slug = nameof(UpdateCollection_FailsToUpdateCollection_WhenMovingStorageCollectionToIIIF);
        
        // Arrange
        var initialCollection = new Collection()
        {
            Id = id,
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
            Hierarchy = [
                new Hierarchy
                {
                    Slug = slug,
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection
                }
            ]
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        var updatedCollection = new PresentationCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic
            },
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = "programmatic-child",
            Parent = RootCollection.Id,
            ItemsOrder = 1,
            PresentationThumbnail = "some/location/2",
            Tags = "some, tags, 2"
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", updatedCollection.AsJson());
        SetCorrectEtag(updateRequestMessage, initialCollection);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        responseCollection.Detail.Should().Be("Cannot convert a IIIF collection to a Storage collection");
        responseCollection.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/CannotChangeCollectionType");
    }

    [Fact]
    public async Task UpdateCollection_CreatesCollection_WhenUnknownCollectionIdProvided()
    {
        // Arrange
        var updatedCollection = new PresentationCollection()
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
            $"{Customer}/collections/createFromUpdate", updatedCollection.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);

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
        responseCollection.PartOf.Single().Id.Should().Be("http://localhost/1/collections/root");
        responseCollection.PartOf.Single().Label["en"].Single().Should().Be("repository root");
        responseCollection.Totals.Should().BeEquivalentTo(DescendantCounts.Empty);
        
        var context = (JArray)responseCollection.Context;
        context.First.Value<string>().Should().Be("http://tbc.org/iiif-repository/1/context.json");
        context.Last.Value<string>().Should().Be("http://iiif.io/api/presentation/3/context.json");
    }

    [Fact]
    public async Task UpdateCollection_FailsToCreateCollection_WhenUnknownCollectionWithETag()
    {
        // Arrange
        var updatedCollection = new PresentationCollection()
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
            $"{Customer}/collections/createFromUpdate2", updatedCollection.AsJson());
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue("\"someTag\""));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);
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
                {"en", new() {"update testing"}}
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

        var updatedCollection = new PresentationCollection()
        {
            Behavior =
            [
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            ],
            Slug = "programmatic-child-2",
            Parent = parent
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", JsonSerializer.Serialize(updatedCollection));
        SetCorrectEtag(updateRequestMessage, initialCollection);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);

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
        responseCollection.Totals.Should().BeEquivalentTo(DescendantCounts.Empty);
    }

    [Fact]
    public async Task UpdateCollection_BadRequest_WhenConvertingToStorageCollection()
    {
        // Arrange
        var initialCollection = new Collection()
        {
            Id = "UpdateTester-3",
            UsePath = true,
            Label = new LanguageMap
            {
                {"en", new() {"update testing"}}
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

        var updatedCollection = new PresentationCollection
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
        SetCorrectEtag(updateRequestMessage, initialCollection);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);

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
                {"en", new() {"update testing"}}
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

        var updatedCollection = new PresentationCollection()
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
            $"{Customer}/collections/{initialCollection.Id}", updatedCollection.AsJson());
        SetCorrectEtag(updateRequestMessage, initialCollection);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be("The parent collection could not be found");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ParentCollectionNotFound");
    }

    [Fact]
    public async Task UpdateCollection_FailsToUpdateCollection_WhenParentIsHierarchicalUri()
    {
        // Arrange
        var initialCollection = new Collection
        {
            Id = "UpdateTester-7",
            UsePath = true,
            Label = new()
            {
                {"en", new() {"update testing"}}
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

        var slug = nameof(UpdateCollection_FailsToUpdateCollection_WhenParentIsHierarchicalUri);
        await dbContext.Hierarchy.AddAsync(new()
        {
            CollectionId = "UpdateTester-7",
            Slug = slug,
            Parent = RootCollection.Id,
            Type = ResourceType.StorageCollection,
            CustomerId = 1,
            Canonical = true
        });

        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();

        var updatedCollection = new PresentationCollection
        {
            Behavior = new()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new("en", ["test collection - updated"]),
            Slug = "programmatic-child-3",
            Parent = $"http://localhost/1/{RootCollection.Id}" // note hierarchical form
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", JsonSerializer.Serialize(updatedCollection));
        SetCorrectEtag(updateRequestMessage, initialCollection);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be("The parent collection could not be found");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ParentCollectionNotFound");
    }

    [Fact]
    public async Task UpdateCollection_FailsToUpdateCollection_WhenETagIncorrect()
    {
        var slug = nameof(UpdateCollection_FailsToUpdateCollection_WhenETagIncorrect);
        var updatedCollection = new PresentationCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = slug,
            Parent = parent
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            "1/collections/FirstChildCollection", updatedCollection.AsJson());
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue("\"notReal\""));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);
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
                {"en", new() {"update testing"}}
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
                {"en", new() {"update testing"}}
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

        var updatedCollection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = parentCollection.Hierarchy.Single(h => h.Canonical).Slug,
            Parent = childCollection.Id
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"1/collections/{parentCollection.Id}", updatedCollection.AsJson());
        SetCorrectEtag(updateRequestMessage, parentCollection);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/PossibleCircularReference");
    }
    
    [Fact]
    public async Task UpdateCollection_ReturnsCorrectTitles_IfUpdatingStorageCollectionWithDescendants()
    {
        // Arrange
        var parentIdentifier = nameof(UpdateCollection_ReturnsCorrectTitles_IfUpdatingStorageCollectionWithDescendants);
        var childIdentifier = $"c{parentIdentifier}";

        var expectedCounts = new DescendantCounts(1, 0, 1);
        
        // Create a parent with 2 children: 1 storage + 1 manifest 
        var parentCollection = await dbContext.Collections.AddTestCollection(parentIdentifier);
        await dbContext.Collections.AddTestCollection(childIdentifier, parent: parentIdentifier);
        await dbContext.Manifests.AddTestManifest(childIdentifier, parent: parentIdentifier);
        await dbContext.SaveChangesAsync();

        // doesn't matter what we update - just that we do
        var updatedCollection = new PresentationCollection
        {
            Behavior = [Behavior.IsPublic, Behavior.IsStorageCollection],
            Label = new LanguageMap("en", ["updated"]),
            Slug = parentCollection.Entity.Hierarchy!.Single(h => h.Canonical).Slug,
            Parent = RootCollection.Id,
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"1/collections/{parentIdentifier}", updatedCollection.AsJson());
        SetCorrectEtag(updateRequestMessage, parentCollection.Entity);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var returnedCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();
        returnedCollection.Totals.Should().BeEquivalentTo(expectedCounts);
        returnedCollection.Items.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task UpdateCollection_RemovesErroneousBehaviors()
    {
        // Arrange
        var parentIdentifier = nameof(UpdateCollection_RemovesErroneousBehaviors);
        var childIdentifier = $"c{parentIdentifier}";

        // Create a parent with 2 children: 1 storage + 1 manifest 
        var parentCollection = await dbContext.Collections.AddTestCollection(parentIdentifier);
        await dbContext.Collections.AddTestCollection(childIdentifier, parent: parentIdentifier);
        await dbContext.Manifests.AddTestManifest(childIdentifier, parent: parentIdentifier);
        await dbContext.SaveChangesAsync();

        // doesn't matter what we update - just that we do
        var updatedCollection = new PresentationCollection
        {
            Behavior = [Behavior.IsPublic, Behavior.IsStorageCollection, "I'm fake and will be removed"],
            Label = new LanguageMap("en", ["updated"]),
            Slug = parentCollection.Entity.Hierarchy!.Single(h => h.Canonical).Slug,
            Parent = RootCollection.Id,
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"1/collections/{parentIdentifier}", updatedCollection.AsJson());
        SetCorrectEtag(updateRequestMessage, parentCollection.Entity);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var returnedCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();
        returnedCollection.Behavior.Should()
            .HaveCount(2)
            .And.ContainInOrder(Behavior.IsPublic, Behavior.IsStorageCollection);
    }
    
    [Fact]
    public async Task UpdateCollection_FailsToUpdateCollection_WhenCalledWithoutNeededHeaders()
    {
        // Arrange
        var slug = nameof(UpdateCollection_FailsToUpdateCollection_WhenCalledWithoutNeededHeaders);
        var getRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, "1/collections/FirstChildCollection");
        
        var getResponse = await httpClient.AsCustomer().SendAsync(getRequestMessage);
        
        var updatedCollection = new PresentationCollection()
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection - updated"]),
            Slug = slug,
            Parent = parent
        };

        var updateRequestMessage = new HttpRequestMessage(HttpMethod.Put, "1/collections/FirstChildCollection");
        updateRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer");
        updateRequestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue(getResponse.Headers.ETag!.Tag));
        updateRequestMessage.Content = new StringContent(updatedCollection.AsJson(), Encoding.UTF8,
            new MediaTypeHeaderValue("application/json"));

        // Act
        var response = await httpClient.SendAsync(updateRequestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    
    [Fact]
    public async Task UpdateCollection_FailsToUpdateIiifCollection_WhenInvalidJson()
    {
        // Arrange
        var initialCollection = new Collection()
        {
            Id = "UpdateTester-IIIF-3",
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
            Hierarchy = [
                new Hierarchy
                {
                    Slug = "iiif-update-test-3",
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection
                }
            ]
        };
                
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        var updatedCollection = 
"""
{
  "behavior": [
    "public-iiif"
  ],
  "type": "Collection",
  "label": {
    "en": [
      "test collection - updated"
    ]
  },
  "slug": "iiif-programmatic-child-3",
  "parent": "root",
  "tags": "some, tags, 2",
 "thumbnail": [
    {
      "id": "https://iiif.io/api/image/3.0/example/reference/someRef",
      "type": "Image",
      "format": "image/jpeg",
      "height": 100,
      "width": 100,
    }
  ],
  "homepage": "invalidHomepage",
  "itemsOrder": 1
}
""";
        

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{initialCollection.Id}", updatedCollection);
        SetCorrectEtag(updateRequestMessage, initialCollection);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);
        
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Detail.Should().Be("Could not deserialize collection");
    }
    
    [Fact]
    public async Task UpdateCollection_CreatesNonPublicIIIFCollection_WhenNoBehavior()
    {
        var collectionId = "noBehavior";
        var slug = nameof(UpdateCollection_CreatesNonPublicIIIFCollection_WhenNoBehavior);
        
        // Arrange
        var updatedCollection = new PresentationCollection()
        {
            Label = new LanguageMap("en", ["test collection - create from update"]),
            Slug = slug,
            Parent = parent,
            ItemsOrder = 1,
            PresentationThumbnail = "some/location/2",
            Tags = "some, tags, 2",
        };

        var updateRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{collectionId}", updatedCollection.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(updateRequestMessage);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();

        var id = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        var fromDatabase = dbContext.Collections.First(c => c.Id == id);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.CollectionId == id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fromDatabase.Id.Should().Be(collectionId);
        hierarchyFromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label!.Values.First()[0].Should().Be("test collection - create from update");
        hierarchyFromDatabase.Slug.Should().Be(slug);
        hierarchyFromDatabase.ItemsOrder.Should().Be(1);
        fromDatabase.Thumbnail.Should().Be("some/location/2");
        fromDatabase.Tags.Should().Be("some, tags, 2");
        fromDatabase.IsPublic.Should().BeFalse();
        fromDatabase.IsStorageCollection.Should().BeFalse();
        responseCollection!.View!.PageSize.Should().Be(20);
        responseCollection.View.Page.Should().Be(1);
        responseCollection.View.Id.Should().Contain("?page=1&pageSize=20");
        responseCollection.PartOf.Single().Id.Should().Be("http://localhost/1/collections/root");
        responseCollection.PartOf.Single().Label["en"].Single().Should().Be("repository root");
        responseCollection.Totals.Should().BeNull();
        
        var context = (JArray)responseCollection.Context;
        context.First.Value<string>().Should().Be("http://tbc.org/iiif-repository/1/context.json");
        context.Last.Value<string>().Should().Be("http://iiif.io/api/presentation/3/context.json");
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
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var responseCollection = await response.ReadAsPresentationJsonAsync<IIIF.Presentation.V3.Collection>();

        var fromDatabase = dbContext.Collections.First(c => c.Hierarchy!.Single(h => h.Canonical).Slug == slug);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.Slug == slug);

        var fromS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/collections/{fromDatabase.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        responseCollection!.Items.Should().BeNull();
        responseCollection.Id.Should().Be(requestMessage.RequestUri!.AbsoluteUri);
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
    public async Task CreateCollection_CreatesMultipleNestedIIIFCollection_ViaHierarchicalCollection()
    {
        // Arrange
        var slug = nameof(CreateCollection_CreatesMultipleNestedIIIFCollection_ViaHierarchicalCollection);

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
            $"{Customer}/first-child/second-child/{slug}", collection);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var responseCollection = await response.ReadAsPresentationJsonAsync<IIIF.Presentation.V3.Collection>();

        var fromDatabase = dbContext.Collections.First(c => c.Hierarchy!.Single(h => h.Canonical).Slug == slug);
        var hierarchyFromDatabase = dbContext.Hierarchy.First(h => h.CustomerId == 1 && h.Slug == slug);

        var fromS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/collections/{fromDatabase.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        responseCollection!.Items.Should().BeNull();
        hierarchyFromDatabase.Parent.Should().Be("SecondChildCollection");
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
    ],
""homepage"": [
  {
    ""id"": ""https://www.getty.edu/art/collection/object/103RQQ"",
    ""type"": ""Text"",
    ""label"": {
      ""en"": [
        ""Home page at the Getty Museum Collection""
      ]
    },
    ""format"": ""text/html"",
    ""language"": [
      ""en""
    ]
  }
]
}";

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/{slug}", collection);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var responseCollection = await response.ReadAsPresentationJsonAsync<IIIF.Presentation.V3.Collection>();

        // Assert
        var fromDatabase = dbContext.Collections
            .Include(c => c.Hierarchy)
            .First(c => c.Hierarchy!.Single(h => h.Canonical).Slug == slug);
        var hierarchyFromDatabase = fromDatabase.Hierarchy.Single();

        var fromS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/collections/{fromDatabase.Id}");
        
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        responseCollection.Id.Should().Be("http://localhost/1/iiif-collection-post-2", "Id is hierarchical path");
        responseCollection.Items!.Count.Should().Be(1);
        responseCollection.Thumbnail.Should().NotBeNull();
        responseCollection.Homepage.Should().NotBeNull();
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
    
    [Fact]
    public async Task CreateCollection_CreatesCollection_SavesInS3Correctly()
    {
        // Arrange
        var slug = nameof(CreateCollection_CreatesCollection_SavesInS3Correctly);

        var collection = @"{
   ""type"": ""Collection"",
   ""behavior"": [
       ""public-iiif"", ""auto-advance""
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
    ],
""homepage"": [
  {
    ""id"": ""https://presentation.example.com"",
    ""type"": ""Text"",
    ""label"": {
      ""en"": [
        ""Foo""
      ]
    },
    ""format"": ""text/html"",
    ""language"": [
      ""en""
    ]
  }
]
}";

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/{slug}", collection);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        var responseCollection = await response.ReadAsPresentationJsonAsync<IIIF.Presentation.V3.Collection>();

        // Assert
        var fromDatabase = dbContext.Collections
            .First(c => c.Hierarchy!.Single(h => h.Canonical).Slug == slug);

        var fromS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/collections/{fromDatabase.Id}");
        
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        responseCollection.Id.Should().Be($"http://localhost/1/{slug}", "Id is hierarchical path");
        responseCollection.Items!.Count.Should().Be(1);
        responseCollection.Thumbnail.Should().NotBeNull();
        responseCollection.Homepage.Should().NotBeNull();
        responseCollection.Behavior.Should()
            .ContainSingle(b => b == "auto-advance", "Known presentation behaviours are removed");
        
        var s3Manifest = fromS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Collection>();
        s3Manifest.Id.Should().EndWith(fromDatabase.Id, "Stored Id is flat path");
        (s3Manifest.Context as string).Should()
            .Be("http://iiif.io/api/presentation/3/context.json", "Context set automatically");
    }
    
    private void SetCorrectEtag(HttpRequestMessage requestMessage, Collection dbCollection)
    {
        // This saves some boilerplate by correctly setting Etag in manager and request
        var tag = $"\"{dbCollection.Id}\"";
        etagManager.UpsertETag($"/{Customer}/collections/{dbCollection.Id}", tag);
        requestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue(tag));
    }
}
