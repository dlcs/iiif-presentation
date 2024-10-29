using API.Helpers;
using API.Settings;
using AWS.S3;
using AWS.S3.Models;
using Core.Helpers;
using Core.IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Options;
using Models.Database.Collections;
using Models.Infrastucture;

namespace API.Infrastructure.AWS;

/// <summary>
/// Class containing higher-level functions to aid interacting with S3
/// </summary>
public class IIIFS3Service(
    IBucketWriter bucketWriter,
    IBucketReader bucketReader,
    ILogger<IIIFS3Service> logger,
    IOptionsMonitor<ApiSettings> options)
{
    public Task<T?> ReadIIIFFromS3<T>(IHierarchyResource dbResource,
        CancellationToken cancellationToken) where T : ResourceBase, new() =>
        ReadIIIFFromS3<T>(dbResource.GetResourceBucketKey(), cancellationToken);

    public async Task<T?> ReadIIIFFromS3<T>(string bucketKey,
        CancellationToken cancellationToken) where T : ResourceBase, new()
    {
        var objectFromBucket = await bucketReader.GetObjectFromBucket(
            new(options.CurrentValue.AWS.S3.StorageBucket, bucketKey), cancellationToken);

        if (objectFromBucket.Stream == null || objectFromBucket.Headers.ContentLength == 0)
            return null;

        return await objectFromBucket.Stream.ToPresentation<T>();
    }
    
    /// <summary>
    /// Write IIIF resource to S3 - ensuring @context and Id set
    /// </summary>
    public async Task SaveIIIFToS3(ResourceBase iiifResource, IHierarchyResource dbResource, string flatId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Uploading resource {Customer}:{ResourceId} file to S3", dbResource.CustomerId, dbResource.Id);
        EnsureIIIFValid(iiifResource, flatId);
        var iiifJson = iiifResource.AsJson();
        var item = new ObjectInBucket(options.CurrentValue.AWS.S3.StorageBucket, dbResource.GetResourceBucketKey());
        await bucketWriter.WriteToBucket(item, iiifJson, "application/json", cancellationToken);
    }

    private static void EnsureIIIFValid(ResourceBase iiifResource, string flatId)
    {
        // NOTE(DG): this isn't doing much just now, could serve as extension point for type-specific config prior to
        // writing data to S3
        iiifResource.Id = flatId;
        iiifResource.EnsurePresentation3Context();
        
        RemovePresentationBehaviours(iiifResource);
    }

    private static void RemovePresentationBehaviours(ResourceBase iiifResource)
    {
        var toRemove = new[] { Behavior.IsStorageCollection, Behavior.IsPublic };
        if (iiifResource.Behavior.IsNullOrEmpty()) return;
        
        iiifResource.Behavior = iiifResource.Behavior.Where(b => !toRemove.Contains(b)).ToList();
    }
}