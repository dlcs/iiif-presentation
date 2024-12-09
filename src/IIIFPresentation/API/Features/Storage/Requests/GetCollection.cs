using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Helpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Repository;
using Repository.Collections;
using Repository.Helpers;

namespace API.Features.Storage.Requests;

public class GetCollection(
    int customerId,
    string id,
    int page,
    int pageSize,
    string? orderBy = null,
    bool descending = false) : IRequest<CollectionWithItems>
{
    public int CustomerId { get; } = customerId;

    public string Id { get; } = id;
    public int Page { get; } = page;
    public int PageSize { get; } = pageSize;
    public string? OrderBy { get; } = orderBy;
    public bool Descending { get; } = descending;
}

public class GetCollectionHandler(PresentationContext dbContext, IPathGenerator pathGenerator) 
    : IRequestHandler<GetCollection, CollectionWithItems>
{
    public async Task<CollectionWithItems> Handle(GetCollection request,
        CancellationToken cancellationToken)
    {
        var collection = await dbContext.RetrieveCollectionWithParentAsync(request.CustomerId, request.Id, cancellationToken: cancellationToken);
        
        if (collection is null) return CollectionWithItems.Empty;

        var hierarchy = collection.Hierarchy!.Single(h => h.Canonical);
        
        var items = await dbContext.RetrieveCollectionItems(request.CustomerId, collection.Id)
            .AsOrderedCollectionItemsQuery(request.OrderBy, request.Descending)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken: cancellationToken);

        var total = await dbContext.GetTotalItemCountForCollection(collection, items.Count, request.PageSize,
            request.Page, cancellationToken);
         
        if (hierarchy.Parent != null)
        {
            collection.FullPath =
                await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext, cancellationToken);
        }
        
        // We know the fullPath of parent collection so we can use that as the base for child items
        items.ForEach(item => item.FullPath = pathGenerator.GenerateFullPath(hierarchy, collection));
        
        return new CollectionWithItems(collection, items, total);
    }
}
