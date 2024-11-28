using API.Helpers;
using Models.Database.Collections;
using Models.Database.General;

namespace API.Tests.Helpers;

public class PathGeneratorTests
{
    private readonly IPathGenerator pathGenerator = TestPathGenerator.CreatePathGenerator("base", Uri.UriSchemeHttp);

    [Fact]
    public void GenerateHierarchicalCollectionId_CreatesIdWhenNoFullPath()
    {
        // Arrange
        var collection = new Collection
        {
            Id = "test",
            Hierarchy = GetDefaultHierarchyList()
        };

        // Act
        var id = pathGenerator.GenerateHierarchicalCollectionId(collection);

        // Assert
        id.Should().Be("http://base/0");
    }
    
    [Theory]
    [InlineData("test/slug", "/test")]
    [InlineData("test/test/slug", "/test/test")]
    [InlineData("slug", "")]
    public void GenerateHierarchicalCollectionParent_Correct_ParentId(string fullPath, string outputUrl)
    {
        // Arrange
        var collection = new Collection
        {
            Id = "someId",
            FullPath = fullPath,
        };
        
        var hierarchy = GetDefaultHierarchyList();

        // Act
        var parentId = pathGenerator.GenerateHierarchicalCollectionParent(collection, hierarchy.First());

        // Assert
        parentId.Should().Be($"http://base/0{outputUrl}");
    }
    
    [Fact]
    public void GenerateHierarchicalCollectionId_CreatesIdWhenFullPath()
    {
        // Arrange
        var collection = new Collection
        {
            Id = "test",
            Hierarchy = GetDefaultHierarchyList(),
            FullPath = "top/test"
        };

        // Act
        var id = pathGenerator.GenerateHierarchicalCollectionId(collection);

        // Assert
        id.Should().Be("http://base/0/top/test");
    }
    
    [Fact]
    public void GenerateHierarchicalId_CreatesIdWhenNoFullPath()
    {
        // Arrange
        var hierarchy = new Hierarchy
        {
            Slug = "test"
        };

        // Act
        var id = pathGenerator.GenerateHierarchicalId(hierarchy);

        // Assert
        id.Should().Be("http://base/0");
    }
    
    [Fact]
    public void GenerateHierarchicalId_CreatesIdWhenFullPath()
    {
        // Arrange
        var hierarchy = new Hierarchy
        {
            FullPath = "top/test",
            Slug = "test"
        };

        // Act
        var id = pathGenerator.GenerateHierarchicalId(hierarchy);

        // Assert
        id.Should().Be("http://base/0/top/test");
    }
    
    [Fact]
    public void GenerateFlatCollectionId_CreatesId()
    {
        // Arrange
        var collection = new Collection
        {
            Id = "test",
            Hierarchy = GetDefaultHierarchyList()
        };

        // Act
        var id = pathGenerator.GenerateFlatCollectionId(collection);

        // Assert
        id.Should().Be("http://base/0/collections/test");
    }
    
    [Theory]
    [InlineData(ResourceType.StorageCollection)]
    [InlineData(ResourceType.IIIFCollection)]
    public void GenerateFlatId_Correct_Collection(ResourceType resourceType)
    {
        // Arrange
        var hierarchy = new Hierarchy
        {
            CollectionId = "test",
            Slug = "slug",
            Type = resourceType
        };

        // Act
        var id = pathGenerator.GenerateFlatId(hierarchy);

        // Assert
        id.Should().Be("http://base/0/collections/test");
    }
    
    [Fact]
    public void GenerateFlatId_Correct_Manifest()
    {
        // Arrange
        var hierarchy = new Hierarchy
        {
            ManifestId = "test",
            Slug = "slug",
            Type = ResourceType.IIIFManifest
        };

        // Act
        var id = pathGenerator.GenerateFlatId(hierarchy);

        // Assert
        id.Should().Be("http://base/0/manifests/test");
    }
    
    [Theory]
    [InlineData(ResourceType.StorageCollection)]
    [InlineData(ResourceType.IIIFCollection)]
    [InlineData(ResourceType.IIIFManifest)]
    public void GenerateFlatParentId_Correct(ResourceType resourceType)
    {
        // Arrange
        var hierarchy = new Hierarchy
        {
            Slug = "slug",
            Parent = "parent",
            Type = resourceType
        };

        // Act
        var id = pathGenerator.GenerateFlatParentId(hierarchy);

        // Assert
        id.Should().Be("http://base/0/collections/parent");
    }
    
    [Fact]
    public void GenerateFlatCollectionViewId_CreatesViewId()
    {
        // Arrange
        var collection = new Collection
        {
            Id = "test",
            Hierarchy = GetDefaultHierarchyList()
        };

        // Act
        var id = pathGenerator.GenerateFlatCollectionViewId(collection, 1, 10, "&test");

        // Assert
        id.Should().Be("http://base/0/collections/test?page=1&pageSize=10&test");
    }
    
    [Fact]
    public void GenerateFlatCollectionViewNext_CreatesViewNext()
    {
        // Arrange
        var collection = new Collection
        {
            Id = "test",
            Hierarchy = GetDefaultHierarchyList()
        };

        // Act
        var id = pathGenerator.GenerateFlatCollectionViewNext(collection, 1, 10, "&test");

        // Assert
        id.Should().Be("http://base/0/collections/test?page=2&pageSize=10&test");
    }
    
    [Fact]
    public void GenerateFlatCollectionViewLast_CreatesViewLast()
    {
        // Arrange
        var collection = new Collection()
        {
            Id = "test",
            Hierarchy = GetDefaultHierarchyList()
        };

        // Act
        var id = pathGenerator.GenerateFlatCollectionViewPrevious(collection, 2, 10, "&test");

        // Assert
        id.Should().Be("http://base/0/collections/test?page=1&pageSize=10&test");
    }

    [Fact]
    public void GetResourceBucketKey_Collection_Correct()
    {
        // Arrange
        var collection = new Collection { CustomerId = 99, Id = "parting-ways" };
        const string expected = "99/collections/parting-ways";
        
        // Act
        var actual = collection.GetResourceBucketKey();
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GetResourceBucketKey_Manifest_Correct()
    {
        // Arrange
        var collection = new Manifest { CustomerId = 99, Id = "parting-ways" };
        const string expected = "99/manifests/parting-ways";
        
        // Act
        var actual = collection.GetResourceBucketKey();
        
        // Assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GenerateFullPath_Correct_ParentFullPathNullOrEmpty(string? path)
    {
        // Arrange
        var parentCollection = new Collection { CustomerId = 99, Id = "javelin", FullPath = path};
        var hierarchy = new Hierarchy { Slug = "slug" };
        const string expected = "slug";
        
        // Act
        var actual = pathGenerator.GenerateFullPath(hierarchy, parentCollection);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GenerateFullPath_Correct_ParentFullPathHasValue()
    {
        // Arrange
        var parentCollection = new Collection { CustomerId = 99, Id = "javelin", FullPath = "have/hold/javelin" };
        var hierarchy = new Hierarchy { Slug = "slug" };
        const string expected = "have/hold/javelin/slug";
        
        // Act
        var actual = pathGenerator.GenerateFullPath(hierarchy, parentCollection);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GenerateFullPath_String_Correct_ParentFullPathNullOrEmpty(string? path)
    {
        // Arrange
        var hierarchy = new Hierarchy { Slug = "slug" };
        const string expected = "slug";
        
        // Act
        var actual = pathGenerator.GenerateFullPath(hierarchy, path);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GenerateFullPath_String_Correct_ParentFullPathHasValue()
    {
        // Arrange
        var hierarchy = new Hierarchy { Slug = "slug" };
        const string expected = "have/hold/javelin/slug";
        
        // Act
        var actual = pathGenerator.GenerateFullPath(hierarchy, "have/hold/javelin");
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GenerateFlatManifestId_Correct()
    {
        // Arrange
        var manifest = new Manifest
        {
            Id = "test",
            CustomerId = 123
        };

        // Act
        var id = pathGenerator.GenerateFlatManifestId(manifest);

        // Assert
        id.Should().Be("http://base/123/manifests/test");
    }
    
    [Fact]
    public void GenerateCanvasId_Correct()
    {
        // Arrange
        var canvasPainting = new Models.Database.CanvasPainting
        {
            Id = "test",
            CustomerId = 123
        };

        // Act
        var id = pathGenerator.GenerateCanvasId(canvasPainting);

        // Assert
        id.Should().Be("http://base/123/canvases/test");
    }
    
    private static List<Hierarchy> GetDefaultHierarchyList() =>  [ new() { Slug = "slug" } ];
}