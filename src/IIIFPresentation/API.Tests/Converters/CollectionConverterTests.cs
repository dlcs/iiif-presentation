using API.Converters;
using FluentAssertions;
using IIIF.Presentation.V3.Strings;
using Models.Database.Collections;
using Models.Response;

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
        var hierarchicalCollection = storageRoot.ToHierarchicalCollection(urlRoots);

        // Assert
        hierarchicalCollection.Id.Should().Be("http://base/1/root");
        hierarchicalCollection.Type.Should().Be("Collection");
        hierarchicalCollection.Label.Count.Should().Be(1);
        hierarchicalCollection.Label["en"].Should().Contain("repository root");
    }
    
    [Fact]
    public void ToFlatCollection_ConvertsStorageCollection()
    {
        // Arrange
        var storageRoot = CreateTestStorageRoot();

        // Act
        var flatCollection = storageRoot.ToFlatCollection(urlRoots, pageSize, new List<Item>());

        // Assert
        flatCollection.Id.Should().Be("http://base/1/someId");
        flatCollection.Type.Should().Be("Collection");
        flatCollection.Label.Count.Should().Be(1);
        flatCollection.Label["en"].Should().Contain("repository root");
        flatCollection.Slug.Should().Be("root");
        flatCollection.SeeAlso.Should().HaveCount(2);
        flatCollection.SeeAlso[0].Profile.Should().Contain("Public");
        flatCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        flatCollection.Created.Should().Be(DateTime.MinValue);
    }

    private static Collection CreateTestStorageRoot()
    {
        var storageRoot = new Collection()
        {
            Id = "someId",
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
}