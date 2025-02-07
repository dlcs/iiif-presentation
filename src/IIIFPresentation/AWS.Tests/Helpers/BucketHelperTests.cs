using AWS.Helpers;
using FluentAssertions;

namespace AWS.Tests.Helpers;

public class BucketHelperTests
{
    [Fact]
    public void GetResourceBucketKey_Collection_Correct()
    {
        // Arrange
        var collection = new Models.Database.Collections.Collection { CustomerId = 99, Id = "parting-ways" };
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
        var collection = new Models.Database.Collections.Manifest { CustomerId = 99, Id = "parting-ways" };
        const string expected = "99/manifests/parting-ways";
        
        // Act
        var actual = collection.GetResourceBucketKey();
        
        // Assert
        actual.Should().Be(expected);
    }
}
