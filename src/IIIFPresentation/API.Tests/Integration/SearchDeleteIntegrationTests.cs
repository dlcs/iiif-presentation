using System.Net;
using API.Tests.Integration.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Repository;
using Services.Search;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class SearchDeleteIntegrationTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContextFixture dbFixture;

    public SearchDeleteIntegrationTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbFixture = storageFixture.DbFixture;
        dbFixture.CleanUp();
        var failingSearchSyncService = new ThrowingSearchSyncService();

        httpClient = factory
            .ConfigureBasicIntegrationTestHttpClient(
                storageFixture.DbFixture,
                appFactory => appFactory
                    .WithLocalStack(storageFixture.LocalStackFixture)
                    .WithTestServices(services =>
                    {
                        services.RemoveAll<ISearchSyncService>();
                        services.AddSingleton<ISearchSyncService>(failingSearchSyncService);
                    }));
    }

    [Fact]
    public async Task DeleteManifest_StillSucceeds_WhenSearchDeleteFails()
    {
        await using var dbContext = GetNewDbContext();
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete, $"1/manifests/{dbManifest.Id}",
            dbContext.GetETag(dbManifest));
        var response = await httpClient.AsCustomer().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        await using var assertionContext = GetNewDbContext();
        assertionContext.Manifests.Any(m => m.Id == dbManifest.Id && m.CustomerId == dbManifest.CustomerId).Should().BeFalse();
    }

    private sealed class ThrowingSearchSyncService : ISearchSyncService
    {
        public Task RunOnce(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task TryDeleteResourceDocumentAsync(Models.Database.Collections.IHierarchyResource resource,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated Typesense failure");
    }

    private PresentationContext GetNewDbContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQLConnection"] = dbFixture.ConnectionString
            })
            .Build();
        return IIIFPresentationContextConfiguration.GetNewDbContext(configuration);
    }
}
