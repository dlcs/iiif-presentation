using Models.Database.Collections;
using Models.Database.General;
using Repository.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace Repository.Tests.Helpers;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class CollectionRetrievalTests
{
    private readonly PresentationContext dbContext;
    private const int CustomerId = 9988;
    private static bool initialised;

    public CollectionRetrievalTests(PresentationContextFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;

        if (initialised) return;

        dbContext.Collections.AddRange(new Collection
            {
                Id = "root9988",
                CustomerId = CustomerId,
                Hierarchy =
                [
                    new Hierarchy
                    {
                        Slug = "",
                        Type = ResourceType.StorageCollection,
                        Canonical = true
                    }
                ]
            },
            new Collection
            {
                Id = "Child1",
                CustomerId = CustomerId,
                Hierarchy =
                [
                    new Hierarchy
                    {
                        Slug = "first-child",
                        Parent = "root9988",
                        Type = ResourceType.StorageCollection,
                        Canonical = true
                    }
                ]
            },
            new Collection
            {
                Id = "Child2",
                CustomerId = CustomerId,
                Hierarchy =
                [
                    new Hierarchy
                    {
                        Slug = "second-child",
                        Parent = "Child1",
                        Type = ResourceType.StorageCollection,
                        Canonical = true
                    }
                ]
            },
            new Collection
            {
                Id = "Child3",
                CustomerId = CustomerId,
                Hierarchy =
                [
                    new Hierarchy
                    {
                        Slug = "third-child",
                        Parent = "Child2",
                        Type = ResourceType.StorageCollection,
                        Canonical = true
                    }
                ]
            });

        dbContext.Manifests.Add(
            new Manifest
            {
                Id = "Child4",
                CustomerId = CustomerId,
                Hierarchy =
                [
                    new Hierarchy
                    {
                        Slug = "third-sibling",
                        Parent = "Child2",
                        Type = ResourceType.StorageCollection,
                        Canonical = true
                    }
                ]
            });

        dbContext.SaveChanges();
        initialised = true;
    }

    [Fact]
    public async Task RetrieveFullPathForCollection_ReturnsEmptyString_CollectionNotFound()
    {
        // Arrange
        var collection = new Collection { Id = "not_found", CustomerId = CustomerId };
        
        // Act
        var actual = await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
        
        // Assert
        actual.Should().BeEmpty();
    }
    
    [Fact]
    public async Task RetrieveFullPathForCollection_ReturnsEmptyString_IfCustomerNotFound()
    {
        // Arrange
        var collection = new Collection { Id = "root9988", CustomerId = -99 };
        
        // Act
        var actual = await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
        
        // Assert
        actual.Should().BeEmpty();
    }
    
    [Fact]
    public async Task RetrieveFullPathForCollection_ReturnsEmptyString_IfNoParents()
    {
        // Arrange
        var collection = new Collection { Id = RootCollection.Id, CustomerId = CustomerId };
        
        // Act
        var actual = await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
        
        // Assert
        actual.Should().BeEmpty("'root' is removed from path");
    }
    
    [Theory]
    [InlineData("Child1", "first-child")]
    [InlineData("Child2", "first-child/second-child")]
    [InlineData("Child3", "first-child/second-child/third-child")]
    public async Task RetrieveFullPathForCollection_ReturnsFullItemSlug_IncludingSelf(string collectionId, string expected)
    {
        // Arrange
        var collection = new Collection { Id = collectionId, CustomerId = CustomerId };
    
        // Act
        var actual = await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
    
        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task RetrieveHierarchy_ReturnsNull_HierarchyNotFound()
    {
        // Act
        var actual = await dbContext.RetrieveHierarchy(CustomerId, "not_found");
        
        // Assert
        actual.Should().BeNull();
    }
    
    [Fact]
    public async Task RetrieveHierarchy_ReturnsNull_CustomerNotFound()
    {
        // Act
        var actual = await dbContext.RetrieveHierarchy(-CustomerId, "whatever");
        
        // Assert
        actual.Should().BeNull();
    }
    
    [Fact]
    public async Task RetrieveHierarchy_ReturnsRoot_IfSlugEmpty()
    {
        // Act
        var actual = await dbContext.RetrieveHierarchy(CustomerId, "");
        
        // Assert
        actual.Slug.Should().BeEmpty();
        actual.Collection.Id.Should().Be("root9988");
    }
    
    [Theory]
    [InlineData("Child1", "first-child")]
    [InlineData("Child2", "first-child/second-child")]
    [InlineData("Child3", "first-child/second-child/third-child")]
    public async Task RetrieveHierarchy_ReturnsHierarchyWithCollection_IfFound(string collectionId, string slug)
    {
        // Arrange
        var expectedSlug = slug.Split("/").Last();
        
        // Act
        var actual = await dbContext.RetrieveHierarchy(CustomerId, slug);
        
        // Assert
        actual.Slug.Should().Be(expectedSlug);
        actual.Collection.Id.Should().Be(collectionId);
    }
    
    [Fact]
    public async Task RetrieveHierarchy_ReturnsHierarchyWithManifest_IfFound()
    {
        // Act
        var actual = await dbContext.RetrieveHierarchy(CustomerId, "first-child/second-child/third-sibling");
        
        // Assert
        actual.Slug.Should().Be("third-sibling");
        actual.Manifest.Id.Should().Be("Child4");
    }

    [Fact]
    public async Task ManifestRetrieval_GetFullPath_ReturnsFullPath_IfFound()
    {
        var actual =
            await ManifestRetrieval.RetrieveFullPathForManifest(
                new Manifest {Id = "Child4", CustomerId = CustomerId}, dbContext);

        actual.Should().Be("first-child/second-child/third-sibling");
    }
}