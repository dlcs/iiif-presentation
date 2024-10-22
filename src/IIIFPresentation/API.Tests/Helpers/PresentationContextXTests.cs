﻿using API.Features.Storage.Helpers;
using API.Tests.Integration.Infrastructure;
using Repository;
using Test.Helpers.Integration;

namespace API.Tests.Helpers;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class PresentationContextXTests
{
    private readonly PresentationContext dbContext;

    public PresentationContextXTests(PresentationContextFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbFixture.CleanUp();
    }

    [Fact]
    public void RetrieveCollectionItems_ReturnsCanonicalOnlyManifestAndCollection()
    {
        var result = dbContext.RetrieveCollectionItems(1, "root");
        var expectedSlugs = new[] { "first-child", "iiif-collection", "iiif-manifest", "non-public" };

        result.Count().Should().Be(4);
        result.Select(h => h.Slug).Should().BeEquivalentTo(expectedSlugs);
    }
    
    [Fact]
    public void RetrieveCollectionItems_CanExcludeNonPublicCollections()
    {
        var result = dbContext.RetrieveCollectionItems(1, "root", true);
        var expectedSlugs = new[] { "first-child", "iiif-collection", "iiif-manifest" };

        result.Count().Should().Be(3);
        result.Select(h => h.Slug).Should().BeEquivalentTo(expectedSlugs);
    }

    [Fact]
    public void RetrieveCollectionItems_IncludesRelations()
    {
        var result = dbContext.RetrieveCollectionItems(1, "root").ToList();

        result.Where(r => r.Collection != null).Should().HaveCount(3);
        result.Where(r => r.Manifest != null).Should().HaveCount(1);
        result.Should().AllSatisfy(h =>
        {
            h.Resource.Should().NotBeNull();
            h.ResourceId.Should().NotBeNullOrEmpty();
        });
    }
}