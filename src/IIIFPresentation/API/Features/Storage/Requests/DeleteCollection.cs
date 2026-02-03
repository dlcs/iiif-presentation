using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using Core;
using MediatR;
using Models;
using Models.API.General;
using Repository;

namespace API.Features.Storage.Requests;

public class DeleteCollection (int customerId, string collectionId, string? etag) : IRequest<ResultMessage<DeleteResult, DeleteResourceErrorType>>
{
    public int CustomerId { get; } = customerId;

    public string CollectionId { get; } = collectionId;

    public string? Etag { get; } = etag;
}

public class DeleteCollectionHandler(
    PresentationContext dbContext,
    HierarchyResourceDeleter hierarchyResourceDeleter,
    ILogger<DeleteCollectionHandler> logger)
    : IRequestHandler<DeleteCollection, ResultMessage<DeleteResult, DeleteResourceErrorType>>
{
    public async Task<ResultMessage<DeleteResult, DeleteResourceErrorType>> Handle(DeleteCollection request, CancellationToken cancellationToken)
    {
        logger.LogDebug("Deleting collection {CollectionId} for customer {CustomerId}", request.CollectionId,
            request.CustomerId);
        
        if (request.CollectionId.Equals(KnownCollections.RootCollection, StringComparison.OrdinalIgnoreCase))
        {
            return DeleteErrorHelper.CannotDeleteRootCollection();
        }

        var collection =
            await dbContext.RetrieveCollectionAsync(request.CustomerId, request.CollectionId, true, cancellationToken);
        
        return await hierarchyResourceDeleter.DeleteResource(request.Etag, collection, cancellationToken);
    }
}
