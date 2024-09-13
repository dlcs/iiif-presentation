using API.Features.Storage.Models;
using API.Helpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Repository;

namespace API.Features.Storage.Requests;

public class GetHierarchicalCollection(int customerId, string slug) : IRequest<CollectionWithItems>
{
    public int CustomerId { get; } = customerId;

    public string Slug { get; } = slug;
}

public class GetHierarchicalCollectionHandler(PresentationContext dbContext)
    : IRequestHandler<GetHierarchicalCollection, CollectionWithItems>
{
    public async Task<CollectionWithItems> Handle(GetHierarchicalCollection request,
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
                
        Collection? collection;

        if (request.Slug.Equals(string.Empty))
        {
            collection = await dbContext.Collections.AsNoTracking().FirstOrDefaultAsync(
                s => s.CustomerId == request.CustomerId && s.Parent == null,
                cancellationToken);
        }
        else
        {
            collection =  await dbContext.Collections.FromSqlRaw(query).OrderBy(i => i.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        List<Collection>? items = null;

        if (collection != null)
        {
            items = await dbContext.Collections
                .Where(s => s.CustomerId == request.CustomerId && s.Parent == collection.Id)
                .ToListAsync(cancellationToken: cancellationToken);

            foreach (var item in items)
            {
                item.FullPath = collection.GenerateFullPath(item.Slug);
            }
            
            collection.FullPath = request.Slug;
        }

        return new CollectionWithItems(collection, items, items?.Count ?? 0);
    }
}