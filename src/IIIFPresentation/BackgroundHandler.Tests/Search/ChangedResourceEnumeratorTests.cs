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
    public async Task GetCustomerIdsAsync_ReturnsDistinctCustomerIds()
    {
        dbFixture.CleanUp();
        await using var dbContext = GetNewDbContext();

        await dbContext.Collections.AddTestRootCollection(91);
        await dbContext.Collections.AddTestCollection("customer-91-collection", customer: 91);
        await dbContext.Collections.AddTestRootCollection(92);
        await dbContext.Manifests.AddTestManifest("customer-92-manifest", customer: 92, ingested: true);
        await dbContext.SaveChangesAsync();

        var sut = new ChangedResourceEnumerator(dbContext);

        var customerIds = await sut.GetCustomerIdsAsync();

        customerIds.Should().BeEquivalentTo([91, 92], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetChangedResources_IncludesManifests_WhenLastProcessedChanges_ForCustomer()
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

        var changedResources = await sut.GetChangedResources(customerId, DateTime.UtcNow.AddMinutes(-5));

        changedResources.Should().Contain(new SearchResourceTarget(customerId, collection.Entity.Id, SearchResourceType.StorageCollection));
        changedResources.Should().Contain(new SearchResourceTarget(customerId, manifest.Entity.Id, SearchResourceType.Manifest));
    }

    [Fact]
    public async Task GetAllDocumentIds_ReturnsIdsForSingleCustomer()
    {
        dbFixture.CleanUp();
        await using var dbContext = GetNewDbContext();

        await dbContext.Collections.AddTestRootCollection(70);
        var includedManifest = await dbContext.Manifests.AddTestManifest("included-manifest", customer: 70, ingested: true);
        await dbContext.Collections.AddTestRootCollection(71);
        await dbContext.Manifests.AddTestManifest("excluded-manifest", customer: 71, ingested: true);
        await dbContext.SaveChangesAsync();

        var sut = new ChangedResourceEnumerator(dbContext);

        var ids = await sut.GetAllDocumentIds(70);

        ids.Should().Contain(SearchDocumentId.Generate(70, SearchResourceType.Manifest, includedManifest.Entity.Id));
        ids.Should().OnlyContain(id => id.StartsWith("70:", StringComparison.Ordinal));
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
