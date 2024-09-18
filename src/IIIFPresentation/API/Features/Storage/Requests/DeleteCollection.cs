using Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Models.API.General;
using Repository;

namespace API.Features.Storage.Requests;

public class DeleteCollection (int customerId, string collectionId) : IRequest<ResultMessage<DeleteResult, DeleteCollectionType>>
{
    public int CustomerId { get; } = customerId;

    public string CollectionId { get; } = collectionId;
}

public class DeleteCollectionHandler(
    PresentationContext dbContext,
    ILogger<CreateCollection> logger)
    : IRequestHandler<DeleteCollection, ResultMessage<DeleteResult, DeleteCollectionType>>
{
    private const string RootCollection = "root";
    
    public async Task<ResultMessage<DeleteResult, DeleteCollectionType>> Handle(DeleteCollection request, CancellationToken cancellationToken)
    {
        logger.LogDebug("Deleting collection {CollectionId}", request.CollectionId);
        
        if (request.CollectionId.Equals(RootCollection, StringComparison.OrdinalIgnoreCase))
        {
            return new ResultMessage<DeleteResult, DeleteCollectionType>(DeleteResult.BadRequest,
                DeleteCollectionType.CannotDeleteRootCollection, "Cannot delete a root collection");
        }

        var collection = await dbContext.Collections.FirstOrDefaultAsync(
            c => c.Id == request.CollectionId && c.CustomerId == request.CustomerId,
            cancellationToken: cancellationToken);

        if (collection is null) return new ResultMessage<DeleteResult, DeleteCollectionType>(DeleteResult.NotFound);

        if (collection.Parent is null)
        {
            return new ResultMessage<DeleteResult, DeleteCollectionType>(DeleteResult.BadRequest,
                DeleteCollectionType.CannotDeleteRootCollection, "Cannot delete a root collection");
        }

        var itemCount = await dbContext.Collections.CountAsync(
            c => c.CustomerId == request.CustomerId && c.Parent == collection.Id,
            cancellationToken: cancellationToken);

        if (itemCount != 0)
        {
            return new ResultMessage<DeleteResult, DeleteCollectionType>(DeleteResult.BadRequest,
                DeleteCollectionType.CollectionNotEmpty, "Cannot delete a collection with child items");
        }

        dbContext.Collections.Remove(collection);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "Error attempting to delete collection {CollectionId}", request.CollectionId);
            return new ResultMessage<DeleteResult, DeleteCollectionType>(DeleteResult.Error,
                DeleteCollectionType.Unknown, "Error deleting collection");
        }

        return new ResultMessage<DeleteResult, DeleteCollectionType>(DeleteResult.Deleted);
    }
}