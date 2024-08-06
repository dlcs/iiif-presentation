using System.Net;
using API.Tests.Integration.Infrastucture;
using FluentAssertions;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class StorageRootTests : IClassFixture<PresentationAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    
    public StorageRootTests(PresentationContextFixture dbFixture, PresentationAppFactory<Startup> factory)
    {
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(dbFixture, "API-Test");
    }
    
    [Fact]
    public async Task Get_Root_Returns_EntryPoint()
    {
        // Act
        var response = await httpClient.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}