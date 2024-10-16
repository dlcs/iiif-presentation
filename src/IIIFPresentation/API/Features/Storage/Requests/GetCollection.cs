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
        Collection? collection = await dbContext.RetrieveCollection(request.CustomerId, request.Id, cancellationToken);
        List<Collection>? items = null;
        Hierarchy? hierarchy = null;
        int total = 0;

        if (collection != null)
        {
            hierarchy = await dbContext.RetrieveHierarchyAsync(collection.CustomerId, collection.Id,
                collection.IsStorageCollection ? ResourceType.StorageCollection : ResourceType.IIIFCollection,
                cancellationToken);
            
            total = await dbContext.Hierarchy.CountAsync(
                c => c.CustomerId == request.CustomerId && c.Parent == collection.Id,
                cancellationToken: cancellationToken);
            items = await dbContext.RetrieveHierarchicalItems(request.CustomerId, collection.Id)
                .AsOrderedCollectionQuery(request.OrderBy, request.Descending)
                .Include(c => c.Hierarchy)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken: cancellationToken);
            
            foreach (var item in items)
            { 
                item.FullPath = hierarchy.GenerateFullPath(item.Hierarchy!.Single(h => h.Canonical).Slug);
            }

            if (hierarchy.Parent != null)
            {
                collection.FullPath = CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
            }
        }

        return new CollectionWithItems(collection, hierarchy, items, total);
    }
}