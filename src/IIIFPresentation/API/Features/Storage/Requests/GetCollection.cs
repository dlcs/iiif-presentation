using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Helpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Models.Database.General;
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

public class GetCollectionHandler(PresentationContext dbContext) : IRequestHandler<GetCollection, CollectionWithItems>
{
    public async Task<CollectionWithItems> Handle(GetCollection request,
        CancellationToken cancellationToken)
    {
        var collection = await dbContext.RetrieveCollectionAsync(request.CustomerId, request.Id, cancellationToken: cancellationToken);
        
        if (collection is null) return CollectionWithItems.Empty;

        var hierarchy = collection.Hierarchy!.Single(h => h.Canonical);
        
        var items = await dbContext.RetrieveCollectionItems(request.CustomerId, collection.Id)
            .AsOrderedCollectionQuery(request.OrderBy, request.Descending)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken: cancellationToken);

        var total = await dbContext.GetTotalItemCountForCollection(collection, items.Count, request.PageSize,
            cancellationToken);
            
        foreach (var item in items)
        { 
            item.FullPath = hierarchy.GenerateFullPath(item.Hierarchy!.Single(h => h.Canonical).Slug);
        }

        if (hierarchy.Parent != null)
        {
            collection.FullPath = CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
        }

        return new CollectionWithItems(collection, hierarchy, items, total);
    }
}