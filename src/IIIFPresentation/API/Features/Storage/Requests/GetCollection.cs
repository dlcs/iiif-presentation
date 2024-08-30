using API.Features.Storage.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Repository;
using Repository.Helpers;

namespace API.Features.Storage.Requests;

public class GetCollection(int customerId, string id) : IRequest<CollectionWithItems>
{
    public int CustomerId { get; } = customerId;

    public string Id { get; } = id;
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
        
        IQueryable<Collection>? items = null;

        if (collection != null)
        {
            items = dbContext.Collections.Where(s => s.CustomerId == request.CustomerId && s.Parent == collection.Id);

            foreach (var item in items)
            { 
                item.FullPath = $"{(collection.Parent != null ? $"{collection.Slug}/" : string.Empty)}{item.Slug}";
            }

            if (collection.Parent != null)
            {
                collection.FullPath = CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
            }
        }

        return new CollectionWithItems(collection, items);
    }
}