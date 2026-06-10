using AWS.Helpers;
using FluentAssertions;

namespace AWS.Tests.Helpers;

public class BucketHelperTests
{
    [Theory]
    [InlineData(BucketLocationType.Default, "99/collections/parting-ways")]
    [InlineData(BucketLocationType.Staging, "staging/99/collections/parting-ways")]
    [InlineData(BucketLocationType.Original, "99/collections/parting-ways/original")]
    [InlineData(BucketLocationType.OriginalStaging, "staging/99/collections/parting-ways/original")]
    public void GetResourceBucketKey_Collection_Correct(BucketLocationType locationType, string expected)
    {
        // Arrange
        var collection = new Models.Database.Collections.Collection { CustomerId = 99, Id = "parting-ways" };

        // Act
        var actual = collection.GetResourceBucketKey(locationType);

        // Assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(BucketLocationType.Default, "99/manifests/parting-ways")]
    [InlineData(BucketLocationType.Staging, "staging/99/manifests/parting-ways")]
    [InlineData(BucketLocationType.Original, "99/manifests/parting-ways/original")]
    [InlineData(BucketLocationType.OriginalStaging, "staging/99/manifests/parting-ways/original")]
    public void GetResourceBucketKey_Manifest_Correct(BucketLocationType locationType, string expected)
    {
        // Arrange
        var manifest = new Models.Database.Collections.Manifest { CustomerId = 99, Id = "parting-ways" };

        // Act
        var actual = manifest.GetResourceBucketKey(locationType);

        // Assert
        actual.Should().Be(expected);
    }
}
