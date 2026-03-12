using BackgroundHandler.Tests.infrastructure;
using Microsoft.Extensions.Configuration;
using Repository;
using Services.Search;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace BackgroundHandler.Tests.Search;

[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ChangedResourceEnumeratorTests
{
    private readonly PresentationContextFixture dbFixture;

    public ChangedResourceEnumeratorTests(PresentationContextFixture dbFixture)
    {
        this.dbFixture = dbFixture;
    }

    [Fact]
    public async Task GetDescendants_ReturnsRecursiveChildren_ForChangedCollection()
    {
        dbFixture.CleanUp();
        await using var dbContext = GetNewDbContext();
        var manifest = await dbContext.Manifests.AddTestManifest("descendant-manifest", parent: "FirstChildCollection",
            customer: PresentationContextFixture.CustomerId, ingested: true);
        await dbContext.SaveChangesAsync();

        var sut = new ChangedResourceEnumerator(dbContext);

        var descendants = await sut.GetDescendants(
            new SearchResourceTarget(PresentationContextFixture.CustomerId, "FirstChildCollection",
                SearchResourceType.StorageCollection));

        descendants.Should().Contain(new SearchResourceTarget(PresentationContextFixture.CustomerId, "SecondChildCollection",
            SearchResourceType.StorageCollection));
        descendants.Should().Contain(new SearchResourceTarget(PresentationContextFixture.CustomerId, manifest.Entity.Id,
            SearchResourceType.Manifest));
    }

    [Fact]
    public async Task GetChangedResources_IncludesManifests_WhenLastProcessedChanges()
    {
        dbFixture.CleanUp();
        await using var dbContext = GetNewDbContext();

        const int customerId = 99;
        await dbContext.Collections.AddTestRootCollection(customerId);
        var oldDate = DateTime.UtcNow.AddHours(-2);
        var manifest = await dbContext.Manifests.AddTestManifest("changed-manifest", customer: customerId, createdDate: oldDate,
            ingested: true);
        manifest.Entity.LastProcessed = DateTime.UtcNow;
        manifest.Entity.Modified = oldDate;

        var collection = await dbContext.Collections.AddTestCollection("changed-collection", customer: customerId, createdDate: oldDate);
        collection.Entity.Modified = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var sut = new ChangedResourceEnumerator(dbContext);

        var changedResources = await sut.GetChangedResources(DateTime.UtcNow.AddMinutes(-5));

        changedResources.Should().Contain(new SearchResourceTarget(customerId, collection.Entity.Id, SearchResourceType.StorageCollection));
        changedResources.Should().Contain(new SearchResourceTarget(customerId, manifest.Entity.Id, SearchResourceType.Manifest));
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
