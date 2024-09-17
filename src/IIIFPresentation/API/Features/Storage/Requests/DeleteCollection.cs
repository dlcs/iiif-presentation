﻿using API.Features.Storage.Models;
using Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Models.API.General;
using Repository;

namespace API.Features.Storage.Requests;

public class DeleteCollection (int customerId, string collectionId) : IRequest<ResultMessage<DeleteResult>>
{
    public int CustomerId { get; } = customerId;

    public string CollectionId { get; } = collectionId;
}

public class DeleteCollectionHandler(
    PresentationContext dbContext,
    ILogger<CreateCollection> logger)
    : IRequestHandler<DeleteCollection, ResultMessage<DeleteResult>>
{
    private const string RootCollection = "root";
    
    public async Task<ResultMessage<DeleteResult>> Handle(DeleteCollection request, CancellationToken cancellationToken)
    {
        logger.LogDebug("Deleting collection {CollectionId}", request.CollectionId);
        
        if (request.CollectionId.Equals(RootCollection, StringComparison.OrdinalIgnoreCase))
        {
            return new ResultMessage<DeleteResult>(DeleteResult.BadRequest, "Cannot delete a root collection",
                (int)DeleteCollectionType.CannotDeleteRootCollection);
        }

        var collection = await dbContext.Collections.FirstOrDefaultAsync(
            c => c.Id == request.CollectionId && c.CustomerId == request.CustomerId,
            cancellationToken: cancellationToken);

        if (collection is null) return new ResultMessage<DeleteResult>(DeleteResult.NotFound);

        if (collection.Parent is null)
        {
            return new ResultMessage<DeleteResult>(DeleteResult.BadRequest, "Cannot delete a root collection",
                (int)DeleteCollectionType.CannotDeleteRootCollection);
        }

        var itemCount = await dbContext.Collections.CountAsync(
            c => c.CustomerId == request.CustomerId && c.Parent == collection.Id,
            cancellationToken: cancellationToken);

        if (itemCount != 0)
        {
            return new ResultMessage<DeleteResult>(DeleteResult.BadRequest,
                "Cannot delete a collection with child items", (int)DeleteCollectionType.CollectionNotEmpty);
        }

        dbContext.Collections.Remove(collection);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "Error attempting to delete collection {CollectionId}", request.CollectionId);
            return new ResultMessage<DeleteResult>(DeleteResult.Error, "Error deleting collection", (int)DeleteCollectionType.Unknown);
        }

        return new ResultMessage<DeleteResult>(DeleteResult.Deleted);
    }
}