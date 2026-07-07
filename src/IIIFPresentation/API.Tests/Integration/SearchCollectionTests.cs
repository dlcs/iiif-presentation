#nullable disable

using System.Net;
using API.Tests.Integration.Infrastructure;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class SearchCollectionTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;

    public SearchCollectionTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));
        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task Search_ReturnsUnauthorized_WhenCalledWithoutAuth()
    {
        // Act
        var response = await httpClient.GetAsync($"1/collections/{RootCollection.Id}/search?label=medicine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_ReturnsNotFound_WhenCollectionNotRoot()
    {
        // Act
        var response = await httpClient.AsCustomer().GetAsync("1/collections/not-root/search?label=medicine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_ReturnsBadRequest_WhenLabelMissing()
    {
        // Act
        var response = await httpClient.AsCustomer().GetAsync($"1/collections/{RootCollection.Id}/search");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("  ab  ")]
    public async Task Search_ReturnsBadRequest_WhenLabelBelowMinimumLength(string label)
    {
        // Act
        var response =
            await httpClient.AsCustomer().GetAsync($"1/collections/{RootCollection.Id}/search?label={label}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("medicine")]
    public async Task Search_ReturnsNotImplemented_WhenLabelMeetsMinimumLength(string label)
    {
        // Act
        var response =
            await httpClient.AsCustomer().GetAsync($"1/collections/{RootCollection.Id}/search?label={label}");

        // Assert - placeholder until label search is implemented
        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }
}
