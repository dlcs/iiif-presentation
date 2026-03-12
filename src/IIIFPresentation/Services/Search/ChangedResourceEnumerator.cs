using Microsoft.EntityFrameworkCore;
using Models.Database.General;
using Repository;
using Repository.Helpers;

namespace Services.Search;

public class ChangedResourceEnumerator(PresentationContext dbContext) : IChangedResourceEnumerator
{
    public async Task<IReadOnlyList<int>> GetCustomerIdsAsync(CancellationToken cancellationToken = default)
    {
        var collectionCustomerIds = await dbContext.Collections.AsNoTracking()
            .Select(c => c.CustomerId)
            .ToListAsync(cancellationToken);

        var manifestCustomerIds = await dbContext.Manifests.AsNoTracking()
            .Select(m => m.CustomerId)
            .ToListAsync(cancellationToken);

        return collectionCustomerIds
            .Concat(manifestCustomerIds)
            .Distinct()
            .OrderBy(customerId => customerId)
            .ToList();
    }

    public async IAsyncEnumerable<IReadOnlyList<SearchResourceTarget>> GetAllResources(int customerId, int batchSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var offset = 0;
        while (true)
        {
            var collections = await dbContext.Collections.AsNoTracking()
                .Where(c => c.CustomerId == customerId)
                .OrderBy(c => c.CustomerId).ThenBy(c => c.Id)
                .Skip(offset)
                .Take(batchSize)
                .Select(c => new SearchResourceTarget(c.CustomerId, c.Id,
                    c.IsStorageCollection ? SearchResourceType.StorageCollection : SearchResourceType.IiifCollection))
                .ToListAsync(cancellationToken);

            if (collections.Count == 0) break;

            yield return collections;
            offset += collections.Count;
        }

        offset = 0;
        while (true)
        {
            var manifests = await dbContext.Manifests.AsNoTracking()
                .Where(m => m.CustomerId == customerId)
                .OrderBy(m => m.CustomerId).ThenBy(m => m.Id)
                .Skip(offset)
                .Take(batchSize)
                .Select(m => new SearchResourceTarget(m.CustomerId, m.Id, SearchResourceType.Manifest))
                .ToListAsync(cancellationToken);

            if (manifests.Count == 0) break;

            yield return manifests;
            offset += manifests.Count;
        }
    }

    public async Task<IReadOnlyList<SearchResourceTarget>> GetChangedResources(int customerId, DateTime changedSince,
        CancellationToken cancellationToken = default)
    {
        var collections = await dbContext.Collections.AsNoTracking()
            .Where(c => c.CustomerId == customerId && c.Modified >= changedSince)
            .Select(c => new SearchResourceTarget(c.CustomerId, c.Id,
                c.IsStorageCollection ? SearchResourceType.StorageCollection : SearchResourceType.IiifCollection))
            .ToListAsync(cancellationToken);

        var manifests = await dbContext.Manifests.AsNoTracking()
            .Where(m => m.CustomerId == customerId &&
                        (m.Modified >= changedSince || (m.LastProcessed.HasValue && m.LastProcessed >= changedSince)))
            .Select(m => new SearchResourceTarget(m.CustomerId, m.Id, SearchResourceType.Manifest))
            .ToListAsync(cancellationToken);

        return collections.Concat(manifests)
            .DistinctBy(SearchDocumentId.Generate)
            .ToList();
    }

    public async Task<IReadOnlyList<SearchResourceTarget>> GetDescendants(SearchResourceTarget collectionTarget,
        CancellationToken cancellationToken = default)
    {
        if (collectionTarget.ResourceType == SearchResourceType.Manifest)
        {
            return [];
        }

        var descendants = await dbContext.Hierarchy
            .FromSql($"""
                      WITH RECURSIVE descendant_tree AS (
                          SELECT
                              id,
                              collection_id,
                              manifest_id,
                              parent,
                              slug,
                              customer_id,
                              items_order,
                              canonical,
                              type
                          FROM hierarchy
                          WHERE parent = {collectionTarget.FlatId}
                            AND customer_id = {collectionTarget.CustomerId}
                            AND canonical = true

                          UNION ALL

                          SELECT
                              child.id,
                              child.collection_id,
                              child.manifest_id,
                              child.parent,
                              child.slug,
                              child.customer_id,
                              child.items_order,
                              child.canonical,
                              child.type
                          FROM hierarchy child
                          INNER JOIN descendant_tree dt
                              ON child.parent = dt.collection_id
                          WHERE child.customer_id = {collectionTarget.CustomerId}
                            AND child.canonical = true
                      )
                      SELECT * FROM descendant_tree
                      """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var collectionIds = descendants.Where(d => d.CollectionId != null).Select(d => d.CollectionId!).Distinct().ToList();
        var storageFlags = await dbContext.Collections.AsNoTracking()
            .Where(c => c.CustomerId == collectionTarget.CustomerId && collectionIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.IsStorageCollection, cancellationToken);

        return descendants
            .Select(d => d switch
            {
                { ManifestId: not null } => new SearchResourceTarget(d.CustomerId, d.ManifestId, SearchResourceType.Manifest),
                { CollectionId: not null } when storageFlags.GetValueOrDefault(d.CollectionId) =>
                    new SearchResourceTarget(d.CustomerId, d.CollectionId, SearchResourceType.StorageCollection),
                { CollectionId: not null } => new SearchResourceTarget(d.CustomerId, d.CollectionId, SearchResourceType.IiifCollection),
                _ => null
            })
            .OfType<SearchResourceTarget>()
            .DistinctBy(SearchDocumentId.Generate)
            .ToList();
    }

    public async Task<IReadOnlyCollection<string>> GetAllDocumentIds(int customerId, CancellationToken cancellationToken = default)
    {
        var collectionIds = await dbContext.Collections.AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => SearchDocumentId.Generate(c.CustomerId,
                c.IsStorageCollection ? SearchResourceType.StorageCollection : SearchResourceType.IiifCollection, c.Id))
            .ToListAsync(cancellationToken);

        var manifestIds = await dbContext.Manifests.AsNoTracking()
            .Where(m => m.CustomerId == customerId)
            .Select(m => SearchDocumentId.Generate(m.CustomerId, SearchResourceType.Manifest, m.Id))
            .ToListAsync(cancellationToken);

        return collectionIds.Concat(manifestIds).ToArray();
    }
}
