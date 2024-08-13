using MediatR;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Repository;

namespace API.Features.Storage.Requests;

public class GetHierarchicalCollection : IRequest<(Collection? root, IQueryable<Collection>? items)>
{
    public GetHierarchicalCollection(int customerId, string slug)
    {
        CustomerId = customerId;
        Slug = slug;
    }

    public int CustomerId { get; }
    
    public string Slug { get; }
}

public class GetHierarchicalCollectionHandler : IRequestHandler<GetHierarchicalCollection, (Collection? root, IQueryable<Collection>? items)>
{
    private readonly PresentationContext dbContext;

    public GetHierarchicalCollectionHandler(PresentationContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<(Collection? root, IQueryable<Collection>? items)> Handle(GetHierarchicalCollection request,
        CancellationToken cancellationToken)
    {
                var query = $@"
WITH RECURSIVE tree_path AS (
    SELECT
        id,
        parent,
        slug,
        customer_id,
        created,
        modified,
        created_by,
        modified_by,
        is_public,
        is_storage_collection,
        items_order,
        label,
        thumbnail,
        locked_by,
        tags,
        use_path,
        1 AS level,
        slug_array,
        array_length(slug_array, 1) AS max_level
    FROM
        (SELECT
             id,
             parent,
             slug,
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
             string_to_array('/{request.Slug}', '/') AS slug_array
         FROM
             collections
         WHERE
             slug = (string_to_array('/{request.Slug}', '/'))[1]
           AND parent IS NULL) AS initial_query

    UNION ALL
    SELECT
        t.id,
        t.parent,
        t.slug,
        t.customer_id,
        t.created,
        t.modified,
        t.created_by,
        t.modified_by,
        t.is_public,
        t.is_storage_collection,
        t.items_order,
        t.label,
        t.locked_by,
        t.tags,
        t.thumbnail,
        t.use_path,
        tp.level + 1 AS level,
        tp.slug_array,
        tp.max_level
    FROM
        collections t
            INNER JOIN
        tree_path tp ON t.parent = tp.id
    WHERE
        tp.level < tp.max_level
        AND t.slug = tp.slug_array[tp.level + 1]
        AND t.customer_id = {request.CustomerId}
)
SELECT
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
    slug
FROM
    tree_path
WHERE
    level = max_level
  AND slug = slug_array[max_level]
  AND tree_path.customer_id = {request.CustomerId}";

        var storage = await dbContext.Collections.FromSqlRaw(query).OrderBy(i => i.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);

        IQueryable<Collection> items = null;

        if (storage != null)
        {
            items = dbContext.Collections.Where(s => s.CustomerId == request.CustomerId && s.Parent == storage.Id);

            foreach (var item in items)
            {
                item.FullPath = $"{storage.Slug}/{item.Slug}";
            }
            
            storage.FullPath = request.Slug;
        }

        return (storage, items);
    }
}