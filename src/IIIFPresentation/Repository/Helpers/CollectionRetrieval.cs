using Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Models.Database.General;

namespace Repository.Helpers;

public static class CollectionRetrieval
{
    /// <summary>
    /// For given collection, return the full hierarchical path for it, delimited by `/`
    /// This will consist of the slug of searched for given collection and all it's parents.
    /// </summary>
    /// <param name="collection">Collection to retrieve full path for</param>
    /// <param name="dbContext">Current db context</param>
    /// <returns>Delimited path</returns>
    /// <exception cref="PresentationException">Thrown if a circular dependency is expected</exception>
    /// <remarks>Note that both 'root' level items and not-found items will return empty string</remarks>
    public static async Task<string> RetrieveFullPathForCollection(Collection collection, PresentationContext dbContext, CancellationToken cancellationToken = default)
    {
        var query = $@"
WITH RECURSIVE parentsearch AS (
 select
    id,
    collection_id,
    manifest_id,
    parent,
    customer_id,
    items_order,
    slug,
    canonical,
    type,
    0 AS generation_number
 FROM hierarchy
 WHERE collection_id = '{collection.Id}'
 UNION
 SELECT
    child.id,
    child.collection_id,
    child.manifest_id,
    child.parent,
    child.customer_id,
    child.items_order,
    child.slug,
    child.canonical,
    child.type,
    generation_number+1 AS generation_number
 FROM hierarchy child
     JOIN parentsearch ps ON child.collection_id=ps.parent
 WHERE generation_number <= 1000 AND child.customer_id = {collection.CustomerId}
)
SELECT * FROM parentsearch ps
         ORDER BY generation_number DESC
";
        var parentCollections = await dbContext.Hierarchy
            .FromSqlRaw(query)
            .ToListAsync(cancellationToken);

        if (parentCollections.Count >= 1000)
        {
            throw new PresentationException("Parent to child relationship exceeds 1000 records");
        }

        var fullPath = string.Join('/', parentCollections
            .Where(parent => !string.IsNullOrEmpty(parent.Parent))
            .Select(parent => parent.Slug));

        return fullPath;
    }

    /// <summary>
    /// For provided slug elements, return `<see cref="Hierarchy"/> and related resource.
    /// slug should be full hierarchical path, e.g.
    ///   "child"
    ///   "parent/child"
    ///   "grandparent/parent/child"
    /// </summary>
    public static async Task<Hierarchy?> RetrieveHierarchy(this PresentationContext dbContext, int customerId, 
        string slug, CancellationToken cancellationToken = default)
    {
        var query = $@"
WITH RECURSIVE tree_path AS (
    SELECT
        id,
        collection_id,
        manifest_id,
        parent,
        slug,
        customer_id,
        items_order,
        canonical,
        type,
    1 AS level,
        slug_array,
        array_length(slug_array, 1) AS max_level
    FROM
        (SELECT
             id,
             collection_id,
             manifest_id,
             parent,
             slug,
             customer_id,
             items_order,
             canonical,
             type,
             string_to_array('/{slug}', '/') AS slug_array
         FROM
             hierarchy
         WHERE
             slug = (string_to_array('/{slug}', '/'))[1]
           AND parent IS NULL) AS initial_query

    UNION ALL
    SELECT
        t.id,
        t.collection_id,
        t.manifest_id,
        t.parent,
        t.slug,
        t.customer_id,
        t.items_order,
        t.canonical,
        t.type,
        tp.level + 1 AS level,
        tp.slug_array,
        tp.max_level
    FROM
        hierarchy t
            INNER JOIN
        tree_path tp ON t.parent = tp.collection_id
    WHERE
        tp.level < tp.max_level
        AND t.slug = tp.slug_array[tp.level + 1]
        AND t.customer_id = {customerId}
)
SELECT
    tree_path.id,
    tree_path.collection_id,
    tree_path.manifest_id,
    tree_path.parent,
    tree_path.slug,
    tree_path.customer_id,
    tree_path.items_order,
    tree_path.canonical,
    tree_path.type
FROM
    tree_path
WHERE
    level = max_level
  AND tree_path.slug = slug_array[max_level]
  AND tree_path.customer_id = {customerId}";

        if (slug.Equals(string.Empty))
        {
            return await dbContext.Hierarchy
                .Include(h => h.Collection)
                .Include(h => h.Manifest)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.CustomerId == customerId && s.Parent == null, cancellationToken);
        }

        return await dbContext.Hierarchy
            .FromSqlRaw(query)
            .Include(h => h.Collection)
            .Include(h => h.Manifest)
            .FirstOrDefaultAsync(cancellationToken);
    }
}