using API.Features.Storage.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
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
    public int Page { get; set; } = page;
    public int PageSize { get; set; } = pageSize;
    public string? OrderBy { get; } = orderBy;
    public bool Descending { get; } = descending;
}

public class GetCollectionHandler(PresentationContext dbContext) : IRequestHandler<GetCollection, CollectionWithItems>
{
    private const string RootCollection = "root";

    public async Task<CollectionWithItems> Handle(GetCollection request,
        CancellationToken cancellationToken)
    {
        Collection? collection;
        
        if (request.Id.Equals(RootCollection, StringComparison.OrdinalIgnoreCase))
        {
            collection = await dbContext.Collections.AsNoTracking().FirstOrDefaultAsync(
                s => s.CustomerId == request.CustomerId && s.Parent == null,
                cancellationToken);
        }
        else
        {
            collection = await dbContext.Collections.AsNoTracking().FirstOrDefaultAsync(
                s => s.CustomerId == request.CustomerId && s.Id == request.Id,
                cancellationToken);
        }
        
        List<Collection>? items = null;
        int total = 0;

        if (collection != null)
        {
            total = await dbContext.Collections.CountAsync(
                c => c.CustomerId == request.CustomerId && c.Parent == collection.Id,
                cancellationToken: cancellationToken);
            items = await dbContext.Collections.AsNoTracking()
                .Where(c => c.CustomerId == request.CustomerId && c.Parent == collection.Id)
                .AsOrderedCollectionQuery(request.OrderBy, request.Descending)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken: cancellationToken);
            
            foreach (var item in items)
            { 
                item.FullPath = $"{(collection.Parent != null ? $"{collection.Slug}/" : string.Empty)}{item.Slug}";
            }

            if (collection.Parent != null)
            {
                collection.FullPath = CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
            }
        }

        return new CollectionWithItems(collection, items, total);
    }
}