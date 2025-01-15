using API.Features.Storage.Helpers;
using AWS.Helpers;
using Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
    IIIFS3Service iiifS3,
    ILogger<DeleteManifestHandler> logger)
    : IRequestHandler<DeleteManifest, ResultMessage<DeleteResult, DeleteManifestType>>
{
    public async Task<ResultMessage<DeleteResult, DeleteManifestType>> Handle(DeleteManifest request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Deleting manifest {ManifestId} for customer {CustomerId}", request.ManifestId,
            request.CustomerId);

        var manifest =
            await dbContext.RetrieveManifestAsync(request.CustomerId, request.ManifestId, true, false, cancellationToken);
        
        if (manifest is null) return new(DeleteResult.NotFound);

        dbContext.Manifests.Remove(manifest);

        await iiifS3.DeleteIIIFFromS3(manifest);

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