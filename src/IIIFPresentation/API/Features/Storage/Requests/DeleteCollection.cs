using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Infrastructure.Helpers;
using AWS.Helpers;
using Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
    IIIIFS3Service iiifS3,
    ILogger<DeleteCollectionHandler> logger)
    : IRequestHandler<DeleteCollection, ResultMessage<DeleteResult, DeleteResourceErrorType>>
{
    public async Task<ResultMessage<DeleteResult, DeleteResourceErrorType>> Handle(DeleteCollection request, CancellationToken cancellationToken)
    {
        logger.LogDebug("Deleting collection {CollectionId} for customer {CustomerId}", request.CollectionId,
            request.CustomerId);
        
        if (request.CollectionId.Equals(KnownCollections.RootCollection, StringComparison.OrdinalIgnoreCase))
        {
            return new ResultMessage<DeleteResult, DeleteResourceErrorType>(DeleteResult.BadRequest,
                DeleteResourceErrorType.CannotDeleteRootCollection, "Cannot delete a root collection");
        }

        var collection =
            await dbContext.RetrieveCollectionAsync(request.CustomerId, request.CollectionId, true, cancellationToken);
        
        if (collection is null) return new ResultMessage<DeleteResult, DeleteResourceErrorType>(DeleteResult.NotFound);

        if (!EtagComparer.IsMatch(collection.Etag, request.Etag)) return DeleteErrorHelper.EtagNotMatching();
        
        var hasItems = await dbContext.Hierarchy.AnyAsync(
            c => c.CustomerId == request.CustomerId && c.Parent == collection.Id,
            cancellationToken: cancellationToken);

        if (hasItems)
        {
            return new ResultMessage<DeleteResult, DeleteResourceErrorType>(DeleteResult.BadRequest,
                DeleteResourceErrorType.CollectionNotEmpty, "Cannot delete a collection with child items");
        }

        dbContext.Collections.Remove(collection);

        if (!collection.IsStorageCollection)
        {
            await iiifS3.DeleteIIIFFromS3(collection);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "Error attempting to delete collection {CollectionId} for customer {CustomerId}",
                request.CollectionId, request.CustomerId);
            return new ResultMessage<DeleteResult, DeleteResourceErrorType>(DeleteResult.Error,
                DeleteResourceErrorType.Unknown, "Error deleting collection");
        }

        return new ResultMessage<DeleteResult, DeleteResourceErrorType>(DeleteResult.Deleted);
    }
}
