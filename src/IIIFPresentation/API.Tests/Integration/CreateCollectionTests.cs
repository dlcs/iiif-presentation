using System.Net;
using System.Net.Http.Headers;
using System.Text;
using API.Tests.Integration.Infrastucture;
using Core.Response;
using FluentAssertions;
using IIIF.Presentation.V3.Strings;
using Microsoft.AspNetCore.Mvc.Testing;
using Models.API.Collection;
using Models.API.General;
using Models.Infrastucture;
using Repository;
using Test.Helpers.Integration;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class CreateCollectionTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    
    private readonly PresentationContext dbContext;

    private const int Customer = 1;

    private readonly string parent;
    
    public CreateCollectionTests(PresentationContextFixture dbFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = dbFixture.DbContext;
        
        httpClient = factory.WithConnectionString(dbFixture.ConnectionString)
            .CreateClient(new WebApplicationFactoryClientOptions());

        parent = dbContext.Collections.FirstOrDefault(x => x.CustomerId == Customer && x.Slug == string.Empty)!
            .Id!;
        
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
            Label = new LanguageMap("en", new []{"test collection"}),
            Slug = "programmatic-child",
            Parent = parent
        };

        // Act
        var response = await httpClient.AsCustomer(Customer).PostAsync($"{Customer}/collections",
            new StringContent(JsonSerializer.Serialize(collection), Encoding.UTF8,
                new MediaTypeHeaderValue("application/json")));

        var responseCollection = await response.ReadAsPresentationResponseAsync<FlatCollection>();

        var fromDatabase = dbContext.Collections.First(c => c.Id == responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        fromDatabase.Parent.Should().Be(parent);
        fromDatabase.Label.Values.First()[0].Should().Be("test collection");
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
            Label = new LanguageMap("en", new []{"test collection"}),
            Slug = "first-child",
            Parent = parent
        };

        // Act
        var response = await httpClient.AsCustomer(Customer).PostAsync($"{Customer}/collections",
            new StringContent(JsonSerializer.Serialize(collection), Encoding.UTF8,
                new MediaTypeHeaderValue("application/json")));
        
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest); 
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
            Label = new LanguageMap("en", new []{"test collection"}),
            Slug = "first-child",
            Parent = parent
        };

        // Act
        var response = await httpClient.PostAsync($"{Customer}/collections",
            new StringContent(JsonSerializer.Serialize(collection), Encoding.UTF8,
                new MediaTypeHeaderValue("application/json")));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}