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
}