using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using API.Infrastructure.Requests;
using API.Tests.Integration.Infrastucture;
using Core.Response;
using FluentAssertions;
using IIIF.Presentation.V3.Strings;
using Microsoft.AspNetCore.Mvc.Testing;
using Models.API.Collection;
using Models.Infrastucture;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
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

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer");

        parent = dbContext.Collections.FirstOrDefault(x => x.CustomerId == Customer && x.Slug == string.Empty)!
            .Id!;
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
        var response = await httpClient.PostAsync($"{Customer}/collections",
            new StringContent(JsonSerializer.Serialize(collection), Encoding.UTF8,
                new MediaTypeHeaderValue("application/json")));
        var test = await response.Content.ReadAsStringAsync();

        var stuff = JsonSerializer.Deserialize<ModifyEntityResult<FlatCollection>>(test, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var responseCollection = await response.ReadAsPresentationResponseAsync<ModifyEntityResult<FlatCollection>>(new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });

        var fromDatabase = dbContext.Collections.FirstOrDefault(c => c.Id == responseCollection!.Entity!.Id);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
    }
}