using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Infrastructure.Helpers;
using AWS.Helpers;
using Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Models.API.General;
using Repository;

namespace API.Features.Manifest.Requests;

public class DeleteManifest(int customerId, string manifestId, string? etag)
    : IRequest<ResultMessage<DeleteResult, DeleteResourceErrorType>>
{
    public int CustomerId { get; } = customerId;
    public string ManifestId { get; } = manifestId;
    public string? Etag { get; } = etag;
}

public class DeleteManifestHandler(
    PresentationContext dbContext,
    IIIIFS3Service iiifS3,
    ILogger<DeleteManifestHandler> logger)
    : IRequestHandler<DeleteManifest, ResultMessage<DeleteResult, DeleteResourceErrorType>>
{
    public async Task<ResultMessage<DeleteResult, DeleteResourceErrorType>> Handle(DeleteManifest request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Deleting manifest {ManifestId} for customer {CustomerId}", request.ManifestId,
            request.CustomerId);

        var manifest =
            await dbContext.RetrieveManifestAsync(request.CustomerId, request.ManifestId, true,
                withCanvasPaintings: false, cancellationToken: cancellationToken);
        
        if (manifest is null) return new(DeleteResult.NotFound);
        
        if (!EtagComparer.IsMatch(manifest.Etag, request.Etag)) return DeleteErrorHelper.EtagNotMatching();

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
                DeleteResourceErrorType.Unknown, "Error deleting manifest");
        }
        
        return new(DeleteResult.Deleted);
    }
}
