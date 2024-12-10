using API.Converters;
using API.Helpers;
using API.Tests.Helpers;
using IIIF.Presentation.V3.Strings;
using Microsoft.AspNetCore.Http;
using Models.Database.Collections;
using Models.Database.General;

#nullable disable

namespace API.Tests.Converters;

public class CollectionConverterTests
{
    private const int pageSize = 100;
    
    private readonly IPathGenerator pathGenerator = TestPathGenerator.CreatePathGenerator("base", Uri.UriSchemeHttp);

    [Fact]
    public void ToHierarchicalCollection_ConvertsStorageCollection()
    {
        // Arrange
        var storageRoot = new Collection
        {
            Id = "some-id",
            CustomerId = 1,
            Label = new LanguageMap
            {
                { "en", new List<string> { "repository root" } }
            },
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            Hierarchy = [
                new Hierarchy()
                {
                    Slug = "root"
                }
            ]
        };

        // Act
        var hierarchicalCollection =
            storageRoot.ToHierarchicalCollection(pathGenerator, CreateTestItems());
        // Assert
        hierarchicalCollection.Id.Should().Be("http://base/1");
        hierarchicalCollection.Label!.Count.Should().Be(1);
        hierarchicalCollection.Label["en"].Should().Contain("repository root");
        hierarchicalCollection.Items!.Count.Should().Be(1);
        hierarchicalCollection.Context!.Should().Be("http://iiif.io/api/presentation/3/context.json");
    }
    
    [Fact]
    public void ToHierarchicalCollection_ConvertsStorageCollectionWithFullPath()
    {
        // Arrange
        var storageRoot = CreateTestCollection();

        // Act
        var hierarchicalCollection =
            storageRoot.ToHierarchicalCollection(pathGenerator, CreateTestItems());
        // Assert
        hierarchicalCollection.Id.Should().Be("http://base/1/top/some-id");
        hierarchicalCollection.Label!.Count.Should().Be(1);
        hierarchicalCollection.Label["en"].Should().Contain("repository root");
        hierarchicalCollection.Items!.Count.Should().Be(1);
        hierarchicalCollection.Context!.Should().Be("http://iiif.io/api/presentation/3/context.json");
    }
    
    [Fact]
    public void ToPresentationCollection_ConvertsStorageCollection()
    {
        // Arrange
        var collection = new Collection
        {
            Id = "some-id",
            CustomerId = 1,
            Label = new LanguageMap
            {
                { "en", new List<string> { "repository root" } }
            },
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            Hierarchy = [
                new Hierarchy()
                {
                    CollectionId = "some-id",
                    Slug = "root",
                    CustomerId = 1,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
            
        };

        // Act
        var flatCollection =
            collection.ToPresentationCollection(pageSize, 1, 1, CreateTestItems(), null, pathGenerator);

        // Assert
        flatCollection.Id.Should().Be("http://base/1/collections/some-id");
        flatCollection.FlatId.Should().Be("some-id");
        flatCollection.PublicId.Should().Be("http://base/1");
        flatCollection.Label!.Count.Should().Be(1);
        flatCollection.Label["en"].Should().Contain("repository root");
        flatCollection.Slug.Should().Be("root");
        flatCollection.SeeAlso.Should().HaveCount(2);
        flatCollection.SeeAlso![0].Id.Should().Be("http://base/1");
        flatCollection.SeeAlso![0].Profile.Should().Contain("Public");
        flatCollection.SeeAlso![1].Id.Should().Be("http://base/1/iiif");
        flatCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        flatCollection.Created.Should().Be(DateTime.MinValue);
        flatCollection.Parent.Should().BeNull();
        flatCollection.Items!.Count.Should().Be(1);
        flatCollection.View!.Id.Should().Be("http://base/1/collections/some-id?page=1&pageSize=100");
        flatCollection.View.Next.Should().BeNull();
        flatCollection.View.Last.Should().BeNull();
        flatCollection.PartOf.Should().BeNull("No parent provided");
    }
    
    [Fact]
    public void ToPresentationCollection_ConvertsStorageCollection_WithFullPath()
    {
        // Arrange
        var storageRoot = CreateTestHierarchicalCollection();

        // Act
        var flatCollection =
            storageRoot.ToPresentationCollection(pageSize, 1, 0, CreateTestItems(), null, pathGenerator);

        // Assert
        flatCollection.Id.Should().Be("http://base/1/collections/some-id");
        flatCollection.FlatId.Should().Be("some-id");
        flatCollection.PublicId.Should().Be("http://base/1/top/some-id");
        flatCollection.Label!.Count.Should().Be(1);
        flatCollection.Label["en"].Should().Contain("repository root");
        flatCollection.Slug.Should().Be("root");
        flatCollection.SeeAlso.Should().HaveCount(2);
        flatCollection.SeeAlso![0].Id.Should().Be("http://base/1/top/some-id");
        flatCollection.SeeAlso![0].Profile.Should().Contain("Public");
        flatCollection.SeeAlso![1].Id.Should().Be("http://base/1/top/some-id/iiif");
        flatCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        flatCollection.Created.Should().Be(DateTime.MinValue);
        flatCollection.Parent.Should().Be("http://base/1/collections/top");
        flatCollection.Items!.Count.Should().Be(1);
        flatCollection.View!.Id.Should().Be("http://base/1/collections/some-id?page=1&pageSize=100");
        flatCollection.View.Next.Should().BeNull();
        flatCollection.View.Last.Should().BeNull();
        flatCollection.View.First.Should().BeNull();
        flatCollection.View.Next.Should().BeNull();
        flatCollection.PartOf.Should().BeNull("No parent provided");
    }

    [Fact]
    public void ToPresentationCollection_ConvertsStorageCollection_WithCorrectPaging()
    {
        // Arrange
        var storageRoot = CreateTestHierarchicalCollection();

        // Act
        var flatCollection =
            storageRoot.ToPresentationCollection(1, 2, 3, CreateTestItems(), null, pathGenerator, "orderBy=created");

        // Assert
        flatCollection.Id.Should().Be("http://base/1/collections/some-id");
        flatCollection.FlatId.Should().Be("some-id");
        flatCollection.PublicId.Should().Be("http://base/1/top/some-id");
        flatCollection.Label!.Count.Should().Be(1);
        flatCollection.Label["en"].Should().Contain("repository root");
        flatCollection.Slug.Should().Be("root");
        flatCollection.SeeAlso.Should().HaveCount(2);
        flatCollection.SeeAlso![0].Profile.Should().Contain("Public");
        flatCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        flatCollection.Created.Should().Be(DateTime.MinValue);
        flatCollection.Parent.Should().Be("http://base/1/collections/top");
        flatCollection.Items!.Count.Should().Be(1);
        flatCollection.View!.TotalPages.Should().Be(3);
        flatCollection.View.PageSize.Should().Be(1);
        flatCollection.View.Id.Should().Be("http://base/1/collections/some-id?page=2&pageSize=1&orderBy=created");
        flatCollection.View.Next.Should().Be("http://base/1/collections/some-id?page=3&pageSize=1&orderBy=created");
        flatCollection.View.Previous.Should().Be("http://base/1/collections/some-id?page=1&pageSize=1&orderBy=created");
        flatCollection.View.First.Should().Be("http://base/1/collections/some-id?page=1&pageSize=1&orderBy=created");
        flatCollection.View.Last.Should().Be("http://base/1/collections/some-id?page=3&pageSize=1&orderBy=created");
        flatCollection.TotalItems.Should().Be(3);
        flatCollection.PartOf.Should().BeNull("No parent provided");
    }
    
    [Fact]
    public void ToPresentationCollection_ConvertsStorageCollection_IncludingPartOfForParent()
    {
        // Arrange
        var storageRoot = CreateTestHierarchicalCollection();
        var parentCollection = new Collection { Id = "theparent", Label = new LanguageMap("none", "grace") };

        // Act
        var flatCollection =
            storageRoot.ToPresentationCollection(pageSize, 1, 0, CreateTestItems(), parentCollection, pathGenerator);

        // Assert
        flatCollection.Id.Should().Be("http://base/1/collections/some-id");
        flatCollection.FlatId.Should().Be("some-id");
        flatCollection.PublicId.Should().Be("http://base/1/top/some-id");
        flatCollection.Label!.Count.Should().Be(1);
        flatCollection.Label["en"].Should().Contain("repository root");
        flatCollection.Slug.Should().Be("root");
        flatCollection.SeeAlso.Should().HaveCount(2);
        flatCollection.SeeAlso![0].Id.Should().Be("http://base/1/top/some-id");
        flatCollection.SeeAlso![0].Profile.Should().Contain("Public");
        flatCollection.SeeAlso![1].Id.Should().Be("http://base/1/top/some-id/iiif");
        flatCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        flatCollection.Created.Should().Be(DateTime.MinValue);
        flatCollection.Parent.Should().Be("http://base/1/collections/top");
        flatCollection.Items!.Count.Should().Be(1);
        flatCollection.View!.Id.Should().Be("http://base/1/collections/some-id?page=1&pageSize=100");
        flatCollection.View.Next.Should().BeNull();
        flatCollection.View.Last.Should().BeNull();
        flatCollection.View.First.Should().BeNull();
        flatCollection.View.Next.Should().BeNull();
        var partOf = flatCollection.PartOf.Single();
        partOf.Id.Should().Be("http://base/0/collections/theparent");
        partOf.Label.Should().BeEquivalentTo(parentCollection.Label);
    }

    private static List<Hierarchy> CreateTestItems()
    {
        var items = new List<Hierarchy>
        {
            new()
            {
                CollectionId = "some-child",
                CustomerId = 1,
                Slug = "root",
                Type = ResourceType.StorageCollection,
                Collection = new Collection
                {
                    Id = "someId",
                    IsPublic = true,
                }
            }
        };
        
        return items;
    }
    
    private static Collection CreateTestHierarchicalCollection()
    {
        var collection = new Collection
        {
            Id = "some-id",
            CustomerId = 1,
            Label = new LanguageMap
            {
                { "en", new List<string> { "repository root" } }
            },
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            FullPath = "top/some-id",
            Hierarchy = [
                new Hierarchy()
                {
                    CollectionId = "some-id",
                    Slug = "root",
                    Parent = "top",
                    CustomerId = 1,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };
        
        return collection;
    }
    
    private static Collection CreateTestCollection()
    {
        var collection = new Collection
        {
            Id = "some-id",
            CustomerId = 1,
            Label = new LanguageMap
            {
                { "en", new List<string> { "repository root" } }
            },
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            FullPath = "top/some-id"
        };
        
        return collection;
    }
}
