using MediatR;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Repository;

namespace API.Features.Storage.Requests;

public class GetCollection : IRequest<(Collection? root, IQueryable<Collection>? items)>
{
    public GetCollection(int customerId, string id)
    {
        CustomerId = customerId;
        Id = id;
    }

    public int CustomerId { get; }
    
    public string Id { get; }
}

public class GetCollectionHandler : IRequestHandler<GetCollection, (Collection? root, IQueryable<Collection>? items)>
{
    private readonly PresentationContext dbContext;
    
    private const string RootCollection = "root";

    public GetCollectionHandler(PresentationContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<(Collection? root, IQueryable<Collection>? items)> Handle(GetCollection request,
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
                collection.FullPath = RetrieveFullPath(collection);
            }
        }

        return (collection, items);
    }

    private string RetrieveFullPath(Collection collection)
    {
        var query = $@"
WITH RECURSIVE parentsearch AS (
 select
    id,
    parent,
    customer_id,
    created,
    modified,
    created_by,
    modified_by,
    is_public,
    is_storage_collection,
    items_order,
    label,
    locked_by,
    tags,
    thumbnail,
    use_path,
    slug,
    0 AS generation_number
 FROM collections
 WHERE id = '{collection.Id}'
 UNION
 SELECT
    child.id,
    child.parent,
    child.customer_id,
    child.created,
    child.modified,
    child.created_by,
    child.modified_by,
    child.is_public,
    child.is_storage_collection,
    child.items_order,
    child.label,
    child.locked_by,
    child.tags,
    child.thumbnail,
    child.use_path,
    child.slug,
    generation_number+1 AS generation_number
 FROM collections child
     JOIN parentsearch ps ON child.id=ps.parent
)
SELECT * FROM parentsearch ORDER BY generation_number DESC
";
        var parentCollections = dbContext.Collections.FromSqlRaw(query).OrderBy(i => i.CustomerId).ToList();

        var fullPath = string.Empty;

        foreach (var parent in parentCollections)
        {
            if (!string.IsNullOrEmpty(parent.Parent))
            {
                fullPath += $"/{parent.Slug}";
            }
        }
        
        return fullPath;
    }
}