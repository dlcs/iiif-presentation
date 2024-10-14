using Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Models.Database.General;

namespace Repository.Helpers;

public static class CollectionRetrieval
{
    public static string RetrieveFullPathForCollection(Collection collection, PresentationContext dbContext)
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
     WHERE generation_number <= 1000
)
SELECT * FROM parentsearch ORDER BY generation_number DESC
";
        var parentCollections = dbContext.Hierarchy
            .FromSqlRaw(query)
            .OrderBy(i => i.CustomerId)
            .ToList();

        if (parentCollections.Count >= 1000)
        {
            throw new PresentationException("Parent to child relationship exceeds 1000 records");
        }

        var fullPath = string.Join('/', parentCollections
            .Where(parent => !string.IsNullOrEmpty(parent.Parent))
            .Select(parent => parent.Slug));

        return fullPath;
    }

    public static async Task<Hierarchy?> RetrieveHierarchy(this PresentationContext dbContext,
        int customerId, string slug, CancellationToken cancellationToken)
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
             string_to_array('/{slug}', '/') AS slug_array
         FROM
             collections
         WHERE
             slug = (string_to_array('/{slug}', '/'))[1]
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
        AND t.customer_id = {customerId}
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
  AND tree_path.customer_id = {customerId}";

        Hierarchy? hierarchy;

        if (slug.Equals(string.Empty))
        {
            hierarchy = await dbContext.Hierarchy.AsNoTracking().FirstOrDefaultAsync(
                s => s.CustomerId == customerId && s.Parent == null,
                cancellationToken);
        }
        else
        {
            hierarchy = await dbContext.Hierarchy.FromSqlRaw(query).OrderBy(i => i.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return hierarchy;
    }
}