using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using Core;
using MediatR;
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
    HierarchyResourceDeleter hierarchyResourceDeleter,
    ILogger<DeleteManifestHandler> logger)
    : IRequestHandler<DeleteManifest, ResultMessage<DeleteResult, DeleteResourceErrorType>>
{
    public async Task<ResultMessage<DeleteResult, DeleteResourceErrorType>> Handle(DeleteManifest request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Deleting manifest {ManifestId} for customer {CustomerId}", request.ManifestId,
            request.CustomerId);

        return await hierarchyResourceDeleter.DeleteResource<Models.Database.Collections.Manifest>(request.Etag,
            request.CustomerId, request.ManifestId, cancellationToken);
    }
}
