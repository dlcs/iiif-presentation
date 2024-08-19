using API.Converters;
using FluentAssertions;
using IIIF.Presentation.V3.Strings;
using Models.API.Collection;
using Models.Database.Collections;

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
            storageRoot.ToHierarchicalCollection(urlRoots, new EnumerableQuery<Collection>(CreateTestItems()));
        // Assert
        hierarchicalCollection.Id.Should().Be("http://base/1");
        hierarchicalCollection.Type.Should().Be(PresentationType.Collection);
        hierarchicalCollection.Label.Count.Should().Be(1);
        hierarchicalCollection.Label["en"].Should().Contain("repository root");
        hierarchicalCollection.Items.Count.Should().Be(1);
    }
    
    [Fact]
    public void ToHierarchicalCollection_ConvertsStorageCollectionWithFullPath()
    {
        // Arrange
        var storageRoot = CreateTestCollection();

        // Act
        var hierarchicalCollection =
            storageRoot.ToHierarchicalCollection(urlRoots, new EnumerableQuery<Collection>(CreateTestItems()));
        // Assert
        hierarchicalCollection.Id.Should().Be("http://base/1/top/some-id");
        hierarchicalCollection.Type.Should().Be(PresentationType.Collection);
        hierarchicalCollection.Label.Count.Should().Be(1);
        hierarchicalCollection.Label["en"].Should().Contain("repository root");
        hierarchicalCollection.Items.Count.Should().Be(1);
    }
    
    [Fact]
    public void ToFlatCollection_ConvertsStorageCollection()
    {
        // Arrange
        var storageRoot = CreateTestStorageRoot();

        // Act
        var flatCollection =
            storageRoot.ToFlatCollection(urlRoots, pageSize, new EnumerableQuery<Collection>(CreateTestItems()));

        // Assert
        flatCollection.Id.Should().Be("http://base/1/collections/some-id");
        flatCollection.PublicId.Should().Be("http://base/1");
        flatCollection.Type.Should().Be(PresentationType.Collection);
        flatCollection.Label.Count.Should().Be(1);
        flatCollection.Label["en"].Should().Contain("repository root");
        flatCollection.Slug.Should().Be("root");
        flatCollection.SeeAlso.Should().HaveCount(2);
        flatCollection.SeeAlso[0].Profile.Should().Contain("Public");
        flatCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        flatCollection.Created.Should().Be(DateTime.MinValue);
        flatCollection.Parent.Should().BeNull();
        flatCollection.Items.Count.Should().Be(1);
    }
    
    [Fact]
    public void ToFlatCollection_ConvertsStorageCollection_WithFullPath()
    {
        // Arrange
        var storageRoot = CreateTestCollection();

        // Act
        var flatCollection =
            storageRoot.ToFlatCollection(urlRoots, pageSize, new EnumerableQuery<Collection>(CreateTestItems()));

        // Assert
        flatCollection.Id.Should().Be("http://base/1/collections/some-id");
        flatCollection.PublicId.Should().Be("http://base/1/top/some-id");
        flatCollection.Type.Should().Be(PresentationType.Collection);
        flatCollection.Label.Count.Should().Be(1);
        flatCollection.Label["en"].Should().Contain("repository root");
        flatCollection.Slug.Should().Be("root");
        flatCollection.SeeAlso.Should().HaveCount(2);
        flatCollection.SeeAlso[0].Profile.Should().Contain("Public");
        flatCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        flatCollection.Created.Should().Be(DateTime.MinValue);
        flatCollection.Parent.Should().Be("http://base/1/collections/top");
        flatCollection.Items.Count.Should().Be(1);
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