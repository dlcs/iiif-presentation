using API.Helpers;
using API.Settings;
using AWS.S3;
using AWS.S3.Models;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Options;
using Models.Database.Collections;

namespace API.Infrastructure.AWS;

/// <summary>
/// Class containing higher-level functions to aid interacting with S3
/// </summary>
public class IIIFS3Service(IBucketWriter bucketWriter, ILogger<IIIFS3Service> logger, IOptionsMonitor<ApiSettings> options)
{
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
    }
}