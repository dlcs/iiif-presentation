using AWS.S3.Models;
using FluentAssertions;

namespace AWS.Tests.S3.Models;

public class ObjectInBucketXTests
{
    [Fact]
    public void GetS3Uri_NoKey_Correct()
    {
        var objectInBucket = new ObjectInBucket("my-bucket");

        var s3Uri = objectInBucket.GetS3Uri();

        s3Uri.ToString().Should().Be("s3://my-bucket/");
    }
    
    [Fact]
    public void GetS3Uri_Key_Correct()
    {
        var objectInBucket = new ObjectInBucket("my-bucket", "key/for/item");

        var s3Uri = objectInBucket.GetS3Uri();

        s3Uri.ToString().Should().Be("s3://my-bucket/key/for/item");
    }
    
    [Theory]
    [InlineData("bucket", "bucket", "key", "key", true)]
    [InlineData("bucket", "bucket", null, null, true)]
    [InlineData("bucket1", "bucket", "key", "key", false)]
    [InlineData("bucket", "bucket1", "key", "key", false)]
    [InlineData("bucket", "bucket", "key1", "key", false)]
    [InlineData("bucket", "bucket", "key", "key1", false)]
    [InlineData("bucket", "bucket", null, "key", false)]
    [InlineData("bucket", "bucket", "key", null, false)]
    public void EqualsOperator_Compares_Values(string b1, string b2, string k1, string k2, bool expected)
    {
        var objectInBucket1 = new ObjectInBucket(b1, k1);
        var objectInBucket2 = new ObjectInBucket(b2, k2);

        objectInBucket1.Equals(objectInBucket2).Should().Be(expected);
        (objectInBucket1 == objectInBucket2).Should().Be(expected);
        (objectInBucket1 != objectInBucket2).Should().Be(!expected);
    }
}