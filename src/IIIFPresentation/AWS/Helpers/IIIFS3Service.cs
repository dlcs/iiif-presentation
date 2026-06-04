using AWS.S3;
using AWS.S3.Models;
using AWS.Settings;
using Core.IIIF;
using Core.Settings;
using Core.Streams;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Database.Collections;

namespace AWS.Helpers;

public interface IIIIFS3Service
{
    public Task<T?> ReadIIIFFromS3<T>(IHierarchyResource dbResource, BucketLocationType locationType,
        CancellationToken cancellationToken) where T : ResourceBase, new();

    public Task<T?> ReadIIIFFromS3<T>(string bucketKey, CancellationToken cancellationToken)
        where T : ResourceBase, new();

    public Task SaveIIIFToS3(ResourceBase iiifResource, IHierarchyResource dbResource, string flatId,
        bool saveToStaging, CancellationToken cancellationToken);

    public Task DeleteIIIFFromS3(IHierarchyResource dbResource, bool fromStaging = false);

    public Task SaveToS3(IHierarchyResource dbResource, BucketLocationType locationType,
        string iiifJson, CancellationToken cancellationToken);

    public Task<Stream?> ReadStreamFromS3(IHierarchyResource dbResource,
        BucketLocationType locationType, CancellationToken cancellationToken);
}

/// <summary>
/// Class containing higher-level functions to aid interacting with S3
/// </summary>
public class IIIFS3Service(
    IBucketWriter bucketWriter,
    IBucketReader bucketReader,
    ILogger<IIIFS3Service> logger,
    IOptionsMonitor<AWSSettings> options,
    IOptionsMonitor<BehaviourSettings> behaviour) : IIIIFS3Service
{
    public Task<T?> ReadIIIFFromS3<T>(IHierarchyResource dbResource,
       BucketLocationType locationType, CancellationToken cancellationToken) where T : ResourceBase, new() =>
        ReadIIIFFromS3<T>(dbResource.GetResourceBucketKey(locationType), cancellationToken);
    
    public Task<Stream?> ReadStreamFromS3(IHierarchyResource dbResource,
        BucketLocationType locationType, CancellationToken cancellationToken) =>
        ReadStreamFromS3(dbResource.GetResourceBucketKey(locationType), cancellationToken);

    public async Task<T?> ReadIIIFFromS3<T>(string bucketKey,
        CancellationToken cancellationToken) where T : ResourceBase, new()
    {
        var stream = await ReadStreamFromS3(bucketKey, cancellationToken);
        if (stream == null) return null;
        
        return await stream.ToPresentation<T>(logger: logger);
    }
    
    public async Task<Stream?> ReadStreamFromS3(string bucketKey,
        CancellationToken cancellationToken)
    {
        var item = new ObjectInBucket(options.CurrentValue.S3.StorageBucket, bucketKey);
        var objectFromBucket = await bucketReader.GetObjectFromBucket(item
            , cancellationToken);

        if (objectFromBucket.Stream.IsNull())
            return null;

        return objectFromBucket.Stream;
    }
    
    /// <summary>
    /// Write IIIF resource to S3 - ensuring @context and Id set
    /// </summary>
    public async Task SaveIIIFToS3(ResourceBase iiifResource, IHierarchyResource dbResource, string flatId,
        bool saveToStaging, CancellationToken cancellationToken)
    {
        logger.LogDebug("Uploading resource {Customer}:{ResourceId} file to S3", dbResource.CustomerId, dbResource.Id);
        EnsureIIIFValid(iiifResource, flatId);
        var iiifJson = iiifResource.AsJson();
        await SaveToS3(dbResource, saveToStaging ? BucketLocationType.Staging : BucketLocationType.Default, iiifJson, cancellationToken);
    }

     public async Task SaveToS3(IHierarchyResource dbResource, BucketLocationType locationType,
        string iiifJson, CancellationToken cancellationToken)
    {
        var item = new ObjectInBucket(options.CurrentValue.S3.StorageBucket,
            dbResource.GetResourceBucketKey(locationType));
        await bucketWriter.WriteToBucket(item, iiifJson, "application/json", cancellationToken);
    }

    /// <summary>
    /// Delete IIIF resource from S3
    /// </summary>
    public async Task DeleteIIIFFromS3(IHierarchyResource dbResource, bool fromStaging = false)
    {
        logger.LogDebug("Deleting resource {Customer}:{ResourceId} file from S3{StagingIndicator}",
            dbResource.CustomerId, dbResource.Id, fromStaging ? "[staging]" : string.Empty);
        var item = new ObjectInBucket(options.CurrentValue.S3.StorageBucket,
            dbResource.GetResourceBucketKey(fromStaging ? BucketLocationType.Staging : BucketLocationType.Default));
        var deleteTask = bucketWriter.DeleteFromBucket(item);
        
        if (behaviour.CurrentValue.ShouldHaveStoredOriginal(dbResource.Created))
        {
            var originalItem = new ObjectInBucket(options.CurrentValue.S3.StorageBucket,
                dbResource.GetResourceBucketKey(fromStaging
                    ? BucketLocationType.OriginalStaging
                    : BucketLocationType.Original));
            await bucketWriter.DeleteFromBucket(originalItem);
        }
        
        await deleteTask;
    }

    private static void EnsureIIIFValid(ResourceBase iiifResource, string flatId)
    {
        // NOTE(DG): this isn't doing much just now, could serve as extension point for type-specific config prior to
        // writing data to S3
        iiifResource.Id = flatId;
        iiifResource.EnsurePresentation3Context();
        
        iiifResource.RemovePresentationBehaviours();
    }
}
