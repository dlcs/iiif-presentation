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

/// <summary>
/// Represents the result of a bucket to bucket copy operation
/// </summary>
/// <param name="Result"><see cref="LargeObjectStatus"/> object that represents overall result of the copy</param>
/// <param name="Size">The size of the asset copied</param>
public record LargeObjectCopyResult(LargeObjectStatus Result, long? Size = null)
{
    /// <summary>
    /// Value indicating whether the destination key exists - only set in NotFound responses 
    /// </summary>
    public bool? DestinationExists { get; set; }
}

/// <summary>
/// The overall result of a bucket to bucket copy operation
/// </summary>
public enum LargeObjectStatus
{
    /// <summary>
    /// Default value
    /// </summary>
    Unknown,
    
    /// <summary>
    /// Object was copied successfully
    /// </summary>
    Success,
    
    /// <summary>
    /// Copy operation was cancelled - this may result in incomplete multi-part uploads being left in S3  
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// Any error occurred during copy
    /// </summary>
    Error,
    
    /// <summary>
    /// File exceeded allowed storage limits
    /// </summary>
    FileTooLarge,
    
    /// <summary>
    /// Unable to copy as target file not found
    /// </summary>
    SourceNotFound
}