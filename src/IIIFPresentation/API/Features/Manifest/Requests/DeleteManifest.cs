using API.Features.Storage.Helpers;
using API.Helpers;
using API.Settings;
using AWS.S3;
using AWS.S3.Models;
using Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.API.General;
using Repository;

namespace API.Features.Manifest.Requests;

public class DeleteManifest(int customerId, string manifestId)
    : IRequest<ResultMessage<DeleteResult, DeleteManifestType>>
{
    public int CustomerId { get; } = customerId;
    public string ManifestId { get; } = manifestId;
}

public class DeleteManifestHandler(
    PresentationContext dbContext,
    IBucketWriter bucketWriter,
    IOptionsMonitor<ApiSettings> options,
    ILogger<DeleteManifestHandler> logger)
    : IRequestHandler<DeleteManifest, ResultMessage<DeleteResult, DeleteManifestType>>
{
    public async Task<ResultMessage<DeleteResult, DeleteManifestType>> Handle(DeleteManifest request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Deleting manifest {ManifestId} for customer {CustomerId}", request.ManifestId,
            request.CustomerId);

        var manifest =
            await dbContext.RetrieveManifestAsync(request.CustomerId, request.ManifestId, true, cancellationToken);
        
        if (manifest is null) return new(DeleteResult.NotFound);

        dbContext.Manifests.Remove(manifest);

        var item = new ObjectInBucket(options.CurrentValue.AWS.S3.StorageBucket, manifest.GetResourceBucketKey());
        await bucketWriter.DeleteFromBucket(item);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "Error attempting to delete manifest {ManifestId} for customer {CustomerId}",
                request.ManifestId, request.CustomerId);
            return new(DeleteResult.Error,
                DeleteManifestType.Unknown, "Error deleting manifest");
        }
        
        return new(DeleteResult.Deleted);
    }
}