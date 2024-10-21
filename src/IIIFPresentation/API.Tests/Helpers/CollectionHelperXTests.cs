using API.Converters;
using API.Helpers;
using Core.Helpers;
using Models.Database.Collections;
using Models.Database.General;

namespace API.Tests.Helpers;

public class CollectionHelperXTests
{
    private readonly UrlRoots urlRoots = new UrlRoots()
    {
        BaseUrl = "http://base"
    };

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
        var id = collection.GenerateHierarchicalCollectionId(urlRoots);

        // Assert
        id.Should().Be("http://base/0");
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
        var id = collection.GenerateHierarchicalCollectionId(urlRoots);

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
        var id = collection.GenerateFlatCollectionId(urlRoots);

        // Assert
        id.Should().Be("http://base/0/collections/test");
    }
    
    [Fact]
    public void GenerateFlatCollectionParent_CreatesParentId()
    {
        // Arrange
        var hierarchy = new Hierarchy
        {
            Slug = "slug",
            Parent = "parent"
        };

        // Act
        var id = hierarchy.GenerateFlatCollectionParent(urlRoots);

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
        var id = collection.GenerateFlatCollectionViewId(urlRoots, 1, 10, "&test");

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
        var id = collection.GenerateFlatCollectionViewNext(urlRoots, 1, 10, "&test");

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
        var id = collection.GenerateFlatCollectionViewPrevious(urlRoots, 2, 10, "&test");

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

    [Fact]
    public void GenerateFullPath_Throws_IfHierarchyNull()
    {
        var parentCollection = new Collection { CustomerId = 99, Id = "javelin" };
        var collection = new Collection { CustomerId = 99, Id = "sut" };
        
        Action action = () => collection.GenerateFullPath(parentCollection);
        action.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void GenerateFullPath_Throws_IfHierarchyEmpty()
    {
        var parentCollection = new Collection { CustomerId = 99, Id = "javelin" };
        var collection = new Collection { CustomerId = 99, Id = "sut", Hierarchy = [] };
        
        Action action = () => collection.GenerateFullPath(parentCollection);
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GenerateFullPath_Correct_ParentFullPathNull(string? path)
    {
        // Arrange
        var parentCollection = new Collection { CustomerId = 99, Id = "javelin", FullPath = path};
        var collection = new Collection
        {
            CustomerId = 99, Id = "sut", Hierarchy = [new Hierarchy { Slug = "slug" }]
        };
        const string expected = "slug";
        
        // Act
        var actual = collection.GenerateFullPath(parentCollection);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GenerateFullPath_Correct_ParentFullPathHasValue()
    {
        // Arrange
        var parentCollection = new Collection { CustomerId = 99, Id = "javelin", FullPath = "have/hold/javelin" };
        var collection = new Collection
        {
            CustomerId = 99, Id = "sut", Hierarchy = [new Hierarchy { Slug = "slug" }]
        };
        const string expected = "have/hold/javelin/slug";
        
        // Act
        var actual = collection.GenerateFullPath(parentCollection);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GenerateFullPath_String_Throws_IfHierarchyNull()
    {
        var collection = new Collection { CustomerId = 99, Id = "sut" };
        
        Action action = () => collection.GenerateFullPath("foo");
        action.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void GenerateFullPath_String_Throws_IfHierarchyEmpty()
    {
        var collection = new Collection { CustomerId = 99, Id = "sut", Hierarchy = [] };
        
        Action action = () => collection.GenerateFullPath("foo");
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GenerateFullPath_String_Correct_ParentFullPathNullOrEmpty(string? path)
    {
        // Arrange
        var collection = new Collection
        {
            CustomerId = 99, Id = "sut", Hierarchy = [new Hierarchy { Slug = "slug" }]
        };
        const string expected = "slug";
        
        // Act
        var actual = collection.GenerateFullPath(path);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GenerateFullPath_String_Correct_ParentFullPathHasValue()
    {
        // Arrange
        var collection = new Collection
        {
            CustomerId = 99, Id = "sut", Hierarchy = [new Hierarchy { Slug = "slug" }]
        };
        const string expected = "have/hold/javelin/slug";
        
        // Act
        var actual = collection.GenerateFullPath("have/hold/javelin");
        
        // Assert
        actual.Should().Be(expected);
    }
    
    private static List<Hierarchy> GetDefaultHierarchyList() =>  [ new() { Slug = "slug" } ];
}