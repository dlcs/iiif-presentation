using Models.Database;
using Models.Database.Collections;
using Models.Database.General;
using Models.DLCS;
using Repository.Paths;

namespace Repository.Tests.Paths;

public class PathGeneratorTests
{
    private readonly IPathGenerator pathGenerator = new TestPathGenerator();

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
        var canvasPainting = new CanvasPainting
        {
            Id = "test",
            CustomerId = 123
        };

        // Act
        var id = pathGenerator.GenerateCanvasId(canvasPainting);

        // Assert
        id.Should().Be("http://base/123/canvases/test");
    }
    
    [Fact]
    public void GenerateAnnotationPagesId_Correct()
    {
        // Arrange
        var canvasPainting = new CanvasPainting
        {
            Id = "test",
            CustomerId = 123,
            CanvasOrder = 7564,
        };

        // Act
        var id = pathGenerator.GenerateAnnotationPagesId(canvasPainting);

        // Assert
        id.Should().Be("http://base/123/canvases/test/annopages/7564");
    }
    
    [Fact]
    public void GeneratePaintingAnnotationId_Correct()
    {
        // Arrange
        var canvasPainting = new CanvasPainting
        {
            Id = "test",
            CustomerId = 123,
            CanvasOrder = 7564,
        };

        // Act
        var id = pathGenerator.GeneratePaintingAnnotationId(canvasPainting);

        // Assert
        id.Should().Be("http://base/123/canvases/test/annotations/7564");
    }

    [Fact]
    public void GenerateSpaceUri_Null_IfManifestHasNoSpace()
    {
        var manifest = new Manifest
        {
            Id = "hello",
            CustomerId = 123
        };

        pathGenerator.GenerateSpaceUri(manifest).Should().BeNull("Manifest has no space");
    }
    
    [Fact]
    public void GenerateSpaceUri_Correct_IfManifestHasSpace()
    {
        var manifest = new Manifest
        {
            Id = "hello",
            CustomerId = 123,
            SpaceId = 93
        };

        var expected = new Uri("https://dlcs.test/customers/123/spaces/93");

        pathGenerator.GenerateSpaceUri(manifest).Should().Be(expected);
    }
    
    [Fact]
    public void GenerateAssetUri_Correct_IfCanvasPaintingHasAssetWithThreeSlashes()
    {
        var manifest = new CanvasPainting()
        {
            Id = "hello",
            CustomerId = 123,
            AssetId = new AssetId(5, 4, "12")
        };

        var expected = "https://dlcs.test/customers/5/spaces/4/images/12";

        pathGenerator.GenerateAssetUri(manifest).Should().Be(expected);
    }
    
    [Fact]
    public void GenerateAssetUri_Null_IfCanvasPaintingDoesNotHaveAsset()
    {
        var manifest = new CanvasPainting
        {
            Id = "hello",
            CustomerId = 123,
        };

        pathGenerator.GenerateAssetUri(manifest).Should().BeNull();
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GenerateHierarchicalFromFullPath_Correct_IfNullOrEmptyFullPath(string? fullPath)
    {
        const string expected = "http://base/5";
        pathGenerator.GenerateHierarchicalFromFullPath(5, fullPath)
            .Should().Be(expected, "Full path ignored if empty (which would be for root)");
    }
    
    [Theory]
    [InlineData("path/to/resource")]
    [InlineData("/path/to/resource")]
    public void GenerateHierarchicalFromFullPath_Correct_FullPathProvided(string? fullPath)
    {
        const string expected = "http://base/5/path/to/resource";
        pathGenerator.GenerateHierarchicalFromFullPath(5, fullPath)
            .Should().Be(expected, "full path appended to customer");
    }
    
    private static List<Hierarchy> GetDefaultHierarchyList() =>  [ new() { Slug = "slug" } ];
}

public class TestPathGenerator : PathGeneratorBase
{
    protected override string PresentationUrl => "http://base";
    protected override Uri DlcsApiUrl => new("https://dlcs.test");
}
