using API.Converters;
using FluentAssertions;
using IIIF.Presentation.V3.Strings;
using Models.Database.Collections;

#nullable disable

namespace API.Tests.Converters;

public class CollectionConverterTests
{
    private readonly UrlRoots urlRoots = new UrlRoots()
    {
        BaseUrl = "http://base"
    };

    private const int pageSize = 100;
    

    [Fact]
    public void ToHierarchicalCollection_ConvertsStorageCollection()
    {
        // Arrange
        var storageRoot = CreateTestStorageRoot();

        // Act
        var hierarchicalCollection =
            storageRoot.ToHierarchicalCollection(urlRoots, new List<Collection>(CreateTestItems()));
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
            storageRoot.ToHierarchicalCollection(urlRoots, new List<Collection>(CreateTestItems()));
        // Assert
        hierarchicalCollection.Id.Should().Be("http://base/1/top/some-id");
        hierarchicalCollection.Label!.Count.Should().Be(1);
        hierarchicalCollection.Label["en"].Should().Contain("repository root");
        hierarchicalCollection.Items!.Count.Should().Be(1);
        hierarchicalCollection.Context!.Should().Be("http://iiif.io/api/presentation/3/context.json");
    }
    
    [Fact]
    public void ToFlatCollection_ConvertsStorageCollection()
    {
        // Arrange
        var storageRoot = CreateTestStorageRoot();

        // Act
        var flatCollection =
            storageRoot.ToFlatCollection(urlRoots, pageSize, 1, 1, new List<Collection>(CreateTestItems()));

        // Assert
        flatCollection.Id.Should().Be("http://base/1/collections/some-id");
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
    }
    
    [Fact]
    public void ToFlatCollection_ConvertsStorageCollection_WithFullPath()
    {
        // Arrange
        var storageRoot = CreateTestCollection();

        // Act
        var flatCollection =
            storageRoot.ToFlatCollection(urlRoots, pageSize, 1, 0, new List<Collection>(CreateTestItems()));

        // Assert
        flatCollection.Id.Should().Be("http://base/1/collections/some-id");
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
    }
    
    [Fact]
    public void ToFlatCollection_ConvertsStorageCollection_WithCorrectPaging()
    {
        // Arrange
        var storageRoot = CreateTestCollection();

        // Act
        var flatCollection =
            storageRoot.ToFlatCollection(urlRoots, 1, 2, 3, 
                new List<Collection>(CreateTestItems()), "orderBy=created");

        // Assert
        flatCollection.Id.Should().Be("http://base/1/collections/some-id");
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
        flatCollection.View.Last.Should().Be("http://base/1/collections/some-id?page=1&pageSize=1&orderBy=created");
        flatCollection.TotalItems.Should().Be(3);
    }

    private static Collection CreateTestStorageRoot()
    {
        var storageRoot = new Collection()
        {
            Id = "some-id",
            CustomerId = 1,
            Slug = "root",
            Label = new LanguageMap
            {
                { "en", new List<string> { "repository root" } }
            },
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue
        };
        
        return storageRoot;
    }
    
    private static List<Collection> CreateTestItems()
    {
        var items = new List<Collection>()
        {
            new()
            {
                Id = "some-child",
                CustomerId = 1,
                Slug = "some-child",
                Label = new LanguageMap
                {
                    { "en", new List<string> { "repository root" } }
                },
                Created = DateTime.MinValue,
                Modified = DateTime.MinValue,
                Parent = "some-id",
                FullPath = "top/some-child"
            }
        };
        
        return items;
    }
    
    private static Collection CreateTestCollection()
    {
        var storageRoot = new Collection
        {
            Id = "some-id",
            CustomerId = 1,
            Slug = "root",
            Label = new LanguageMap
            {
                { "en", new List<string> { "repository root" } }
            },
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            Parent = "top",
            FullPath = "top/some-id"
        };
        
        return storageRoot;
    }
}