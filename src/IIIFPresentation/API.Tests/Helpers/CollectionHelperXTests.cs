using API.Helpers;
using Models.Database.Collections;

namespace API.Tests.Helpers;

public class CollectionHelperXTests
{
    [Fact]
    public void GenerateETagCacheKey_Correct_Collection()
    {
        // Arrange
        var collection = new Collection
        {
            Id = "test",
            CustomerId = 123
        };

        // Act
        var id = collection.GenerateETagCacheKey();

        // Assert
        id.Should().Be("/123/collections/test");
    }
    
    [Fact]
    public void GenerateETagCacheKey_Correct_Manifest()
    {
        // Arrange
        var manifest = new Manifest
        {
            Id = "test",
            CustomerId = 123
        };

        // Act
        var id = manifest.GenerateETagCacheKey();

        // Assert
        id.Should().Be("/123/manifests/test");
    }
}
