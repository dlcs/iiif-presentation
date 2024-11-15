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
    ILogger<DeleteManifestHandler> logger)
    : IRequestHandler<DeleteManifest, ResultMessage<DeleteResult, DeleteManifestType>>
{
    #region Implementation of IRequestHandler<in DeleteManifest,ResultMessage<DeleteResult,DeleteManifestType>>

    public async Task<ResultMessage<DeleteResult, DeleteManifestType>> Handle(DeleteManifest request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Deleting collection {ManifestId} for customer {CustomerId}", request.ManifestId,
            request.CustomerId);

        var manifest = await dbContext.Manifests.Include(c => c.Hierarchy).FirstOrDefaultAsync(m =>
            m.Id == request.ManifestId && m.CustomerId == request.CustomerId, cancellationToken);

        if (manifest is null) return new(DeleteResult.NotFound);

        dbContext.Manifests.Remove(manifest);

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

    #endregion
}