using System.Text;
using API.Infrastructure.Requests;
using API.Settings;
using Core;
using Microsoft.EntityFrameworkCore;
using Models.API.General;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using DbManifest = Models.Database.Collections.Manifest;

namespace API.Features.Storage.Helpers;

public static class PresentationContextX
{
    public static Task<PresentationResult?> TrySaveCollection(
        this PresentationContext dbContext,
        int customerId,
        ILogger logger,
        CancellationToken cancellationToken)
        => dbContext.TrySave("collection", customerId, logger, cancellationToken);

    public static async Task<PresentationResult?> TrySave(
        this PresentationContext dbContext,
        string resourceType,
        int customerId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "DB Error saving {ResourceType} for customer {Customer}", resourceType, customerId);

            if (ex.IsCustomerIdSlugParentViolation())
            {
                return PresentationResult.Failure(
                    $"The {resourceType} could not be created due to a duplicate slug value",
                    ModifyCollectionType.DuplicateSlugValue, WriteResult.Conflict);
            }

            if (ex.IsManifestPrimaryKeyViolation())
            {
                return PresentationResult.Failure(
                    "The manifest is currently being created",
                    ModifyCollectionType.ManifestCurrentlyIngesting, WriteResult.Conflict);
            }

            return PresentationResult.Failure(
                $"The {resourceType} could not be created", ModifyCollectionType.Unknown);
        }

        return null;
    }

    /// <summary>
    /// Retrieves a manifest from the database, with the Hierarchy records included
    /// </summary>
    /// <param name="dbContext">The context to pull records from</param>
    /// <param name="manifestId">The manifest to retrieve</param>
    /// <param name="tracked">Whether the resource should be tracked or not</param>
    /// <param name="withCanvasPaintings">Whether the CanvasPaintings records should be included</param>
    /// <param name="withBatches">Whether the Batches records should be included</param>
    /// <param name="withPipelineJobs">Whether PipelineJobs records should be included</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The retrieved collection</returns>
    public static async Task<DbManifest?> RetrieveManifestAsync(this PresentationContext dbContext,
        string manifestId, bool tracked = false, bool withCanvasPaintings = true, bool withBatches = false,
        bool withPipelineJobs = false, CancellationToken cancellationToken = default)
    {
        IQueryable<DbManifest> dbContextManifests = dbContext.Manifests;

        if (withCanvasPaintings)
        {
            dbContextManifests = dbContextManifests.Include(m => m.CanvasPaintings).AsSplitQuery();
        }

        if (withBatches)
        {
            dbContextManifests = dbContextManifests.Include(m => m.Batches);
        }

        if (withPipelineJobs)
        {
            dbContextManifests = dbContextManifests.Include(m => m.PipelineJobs);
        }

        var manifest = await dbContextManifests.Retrieve(manifestId, tracked, cancellationToken);
        return manifest;
    }
    
    /// <summary>
    /// Retrieves a 'full' collection from the database, with the Hierarchy records (including Parent)
    /// </summary>
    /// <param name="dbContext">The context to pull records from</param>
    /// <param name="collectionId">The collection to retrieve</param>
    /// <param name="tracked">Whether the resource should be tracked or not</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The retrieved collection</returns>
    public static Task<Collection?> RetrieveCollectionWithParentAsync(this PresentationContext dbContext,
        string collectionId, bool tracked = false, CancellationToken cancellationToken = default)
    {
        var collections = tracked ? dbContext.Collections : dbContext.Collections.AsNoTracking();
        return collections
            .Include(e => e.Hierarchy)!.ThenInclude(h => h.ParentCollection)
            .FirstOrDefaultAsync(e => e.Id == collectionId, cancellationToken);
    }

    /// <summary>
    /// Retrieves a collection from the database, with the Hierarchy records included
    /// </summary>
    /// <param name="dbContext">The context to pull records from</param>
    /// <param name="customerId">Customer the record is attached to</param>
    /// <param name="collectionId">The collection to retrieve</param>
    /// <param name="tracked">Whether the resource should be tracked or not</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The retrieved collection</returns>
    public static Task<Collection?> RetrieveCollectionAsync(this PresentationContext dbContext, int customerId,
        string collectionId, bool tracked = false, CancellationToken cancellationToken = default)
        => dbContext.Collections.Retrieve(collectionId, tracked, cancellationToken);

    /// <summary>
    /// Retrieves a <see cref="IHierarchyResource"/> from database, with Hierarchy records included
    /// </summary>
    /// <param name="entities">The context to pull records from</param>
    /// <param name="resourceId">The collection/manifest Id to retrieve</param>
    /// <param name="tracked">Whether the resource should be tracked or not</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>The retrieved <see cref="IHierarchyResource"/></returns>
    public static async Task<T?> Retrieve<T>(this IQueryable<T> entities,
        string resourceId, bool tracked = false, CancellationToken cancellationToken = default)
        where T : class, IHierarchyResource
    {
        var resources = tracked ? entities : entities.AsNoTracking();

        return await resources
            .Include(e => e.Hierarchy)
            .FirstOrDefaultAsync(e => e.Id == resourceId, cancellationToken);
    }
    
    /// <summary>
    /// Retrieves child hierarchy items for the parent record - entities are not tracked
    /// </summary>
    /// <param name="dbContext">The context to pull records from</param>
    /// <param name="resourceId">The collection to retrieve child items for</param>
    /// <param name="publicOnly">Whether to return public only resources</param>
    /// <returns>A query containing child collections</returns>
    public static IQueryable<Hierarchy> RetrieveCollectionItems(this PresentationContext dbContext, 
        string resourceId, bool publicOnly = false)
    {
        var hierarchy = dbContext.Hierarchy.AsNoTracking()
            .Include(h => h.Collection)
            .Include(h => h.Manifest)
            .Where(c => c.Canonical && c.Parent == resourceId);

        if (publicOnly)
        {
            hierarchy = hierarchy.Where(c => (c.Collection != null && c.Collection.IsPublic) ||  
                                             (c.Manifest != null && c.Manifest.LastProcessed != null));
        }

        return hierarchy;
    }

    /// <summary>
    /// Searches across all resources (Collections + Manifests) in the customer by label value, regardless of
    /// nesting depth, returning the matching canonical <see cref="Hierarchy"/> rows so they can be shaped into
    /// a Collection via the CollectionConverter. Customer scoping is applied automatically by the global query
    /// filter; the returned query is composable, so callers add ordering/paging on top.
    /// </summary>
    /// <remarks>
    /// Label is jsonb but persisted via a value converter, so a LINQ predicate won't translate. This uses raw
    /// SQL following RFC 0008 (docs/rfcs/0008-search-across-mvp.md): the term is split into whitespace tokens
    /// and, case-insensitively and language-agnostically, ALL tokens must appear within a single label value
    /// (same-value AND). e.g. "Hunter Thompson" matches "Thompson, Hunter" but not "Emma Thompson".
    /// </remarks>
    /// <param name="dbContext">Current db context</param>
    /// <param name="terms">
    /// Search terms, already tokenised - each is matched as a substring. Every term is scanned with an unindexed
    /// ILIKE, so callers must reject terms too short to be selective (see <see cref="ApiSettings.MinSearchLength"/>)
    /// </param>
    public static IQueryable<Hierarchy> SearchCollectionItems(this PresentationContext dbContext, string[] terms)
    {
        var tokens = terms
            .Select(EscapeForILike)
            .Distinct()
            .ToList();

        // Nothing searchable (e.g. all-whitespace) - return an empty, still-composable query
        if (tokens.Count == 0) return dbContext.Hierarchy.Where(_ => false);

        var sql = new StringBuilder(
            """
            SELECT h.* FROM hierarchy h
            LEFT JOIN collections c ON c.id = h.collection_id AND c.customer_id = h.customer_id
            LEFT JOIN manifests m ON m.id = h.manifest_id AND m.customer_id = h.customer_id
            WHERE h.canonical
              AND EXISTS (
                SELECT 1
                FROM jsonb_each(COALESCE(c.label, m.label)) AS kv,
                     LATERAL jsonb_array_elements_text(kv.value) AS val
                WHERE
            """);

        var parameters = new List<object>();
        for (var i = 0; i < tokens.Count; i++)
        {
            sql.Append(i == 0 ? " " : " AND ");
            sql.Append($"val ILIKE {{{parameters.Count}}}");
            parameters.Add($"%{tokens[i]}%");
        }

        sql.Append(')');

        return dbContext.Hierarchy
            .FromSqlRaw(sql.ToString(), parameters.ToArray())
            .AsNoTracking()
            .Include(h => h.Collection)
            .Include(h => h.Manifest);
    }

    // Escapes LIKE/ILIKE wildcards so user input matches literally (default '\' escape char).
    private static string EscapeForILike(string token) =>
        token.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public static async Task<int> GetTotalItemCountForCollection(this PresentationContext dbContext,
        Collection collection, int itemCount, int pageSize, int pageNo, CancellationToken cancellationToken = default)
    {
        int total;
        if (itemCount > 0 && itemCount < pageSize)
        {
            // there can't be more as we've asked for PageSize and got less 
            total = itemCount + (pageNo - 1) * pageSize;
        }
        else
        {
            // if we get PageSize back then there may be more in db
            total = await dbContext.Hierarchy.CountAsync(
                c => c.Parent == collection.Id,
                cancellationToken: cancellationToken);
        }

        return total;
    }
}
