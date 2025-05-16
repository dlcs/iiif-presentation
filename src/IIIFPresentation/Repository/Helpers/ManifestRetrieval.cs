using Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;

namespace Repository.Helpers;

public static class ManifestRetrieval
{
    /// <summary>
    ///     For given manifest, return the full hierarchical path for it, delimited by `/`
    ///     This will consist of the slug of searched for given manifest and all it's parents.
    /// </summary>
    /// <param name="manifest">Manifest to retrieve full path for</param>
    /// <param name="dbContext">Current db context</param>
    /// <returns>Delimited path</returns>
    /// <exception cref="PresentationException">Thrown if a circular dependency is expected</exception>
    /// <remarks>Note that both 'root' level items and not-found items will return empty string</remarks>
    public static Task<string> RetrieveFullPathForManifest(Manifest manifest, PresentationContext dbContext,
        CancellationToken cancellationToken = default) =>
        RetrieveFullPathForManifest(manifest.Id, manifest.CustomerId, dbContext, cancellationToken);

    public static async Task<string> RetrieveFullPathForManifest(string manifestId, int customerId,
        PresentationContext dbContext,
        CancellationToken cancellationToken = default)
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
 WHERE manifest_id = '{manifestId}' AND customer_id = {customerId}
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
 WHERE generation_number <= 1000 AND child.customer_id = {customerId}
)
SELECT * FROM parentsearch ps
         ORDER BY generation_number DESC
";
        var parentCollections = await dbContext.Hierarchy
            .FromSqlRaw(query)
            .ToListAsync(cancellationToken);

        if (parentCollections.Count >= 1000)
            throw new PresentationException("Parent to child relationship exceeds 1000 records");

        var fullPath = string.Join('/', parentCollections
            .Where(parent => !string.IsNullOrEmpty(parent.Parent))
            .Select(parent => parent.Slug));

        return fullPath;
    }
}
