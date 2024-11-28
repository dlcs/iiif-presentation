namespace AWS.S3.Models;

public static class ObjectInBucketX
{
    /// <summary>
    /// Get the full s3:// uri for object in bucket
    /// </summary>
    /// <param name="objectInBucket"><see cref="ObjectInBucket"/> to get s3 uri for</param>
    /// <returns></returns>
    /// <remarks>S3 URIs don't include the Region</remarks>
    public static Uri GetS3Uri(this ObjectInBucket objectInBucket)
        => new($"s3://{objectInBucket.Bucket}/{objectInBucket.Key}");
}