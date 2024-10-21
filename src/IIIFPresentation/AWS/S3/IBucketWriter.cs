using AWS.S3.Models;

namespace AWS.S3;

/// <summary>
/// Interface wrapping write interactions with cloud blob storage.
/// </summary>
public interface IBucketWriter
{
    /// <summary>
    /// Write content from provided string to S3 
    /// </summary>
    /// <returns></returns>
    Task WriteToBucket(ObjectInBucket dest, string content, string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete specified objects underlying storage.
    /// NOTE: This method assumes all objects are in the same bucket.
    /// </summary>
    /// <param name="toDelete">List of objects to delete</param>
    Task DeleteFromBucket(params ObjectInBucket[] toDelete);

    /// <summary>
    /// Delete "folder" from underlying storage, this will be any child elements and optionally, the root.
    /// </summary>
    /// <param name="root">Root object to delete</param>
    /// <param name="removeRoot">Whether to remove the root</param>
    Task DeleteFolder(ObjectInBucket root, bool removeRoot);
}
