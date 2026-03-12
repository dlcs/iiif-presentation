using Models.Database.Collections;
using Newtonsoft.Json.Linq;

namespace Services.Search;

public class SearchSyncService(
    TypesenseSettings settings,
    ITypesenseClient typesenseClient,
    ISearchSyncStateStore stateStore,
    IChangedResourceEnumerator changedResourceEnumerator,
    ISearchDocumentBuilder searchDocumentBuilder,
    ILogger<SearchSyncService> logger) : ISearchSyncService
{
    public async Task RunOnce(CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured) return;

        await stateStore.EnsureCreatedAsync(cancellationToken);

        var alias = await typesenseClient.GetAliasAsync(settings.CollectionAlias, cancellationToken);
        var state = await stateStore.GetStateAsync(cancellationToken);

        if (RequiresBootstrap(alias, state))
        {
            await BootstrapAsync(alias?.CollectionName, cancellationToken);
            return;
        }

        if (state?.ActiveCollection == null) return;

        await RunIncrementalSyncAsync(state, state.ActiveCollection, cancellationToken);
        await RunOrphanSweepAsync(state, state.ActiveCollection, cancellationToken);
    }

    public async Task TryDeleteResourceDocumentAsync(IHierarchyResource resource, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured) return;

        var resourceType = resource switch
        {
            Models.Database.Collections.Collection collection when collection.IsStorageCollection =>
                SearchResourceType.StorageCollection,
            Models.Database.Collections.Collection => SearchResourceType.IiifCollection,
            Manifest => SearchResourceType.Manifest,
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null)
        };

        var documentId = SearchDocumentId.Generate(resource.CustomerId, resourceType, resource.Id);

        try
        {
            await typesenseClient.DeleteDocumentAsync(settings.CollectionAlias, documentId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to delete search document {DocumentId}", documentId);
        }
    }

    private bool RequiresBootstrap(TypesenseAlias? alias, SearchSyncState? state) =>
        alias?.CollectionName == null ||
        state == null ||
        state.SchemaVersion != SearchSchema.Version ||
        !string.Equals(state.ActiveCollection, alias.CollectionName, StringComparison.Ordinal);

    private async Task BootstrapAsync(string? previousCollectionName, CancellationToken cancellationToken)
    {
        var nextCollectionName = SearchSchema.GenerateCollectionName(settings);
        logger.LogInformation("Bootstrapping Typesense collection {CollectionName}", nextCollectionName);

        await typesenseClient.CreateCollectionAsync(SearchSchema.GetSearchCollectionSchema(nextCollectionName),
            cancellationToken: cancellationToken);

        await foreach (var batch in changedResourceEnumerator.GetAllResources(settings.BootstrapBatchSize, cancellationToken))
        {
            var documents = await BuildDocuments(batch, cancellationToken);
            await ImportDocuments(nextCollectionName, documents, cancellationToken);
        }

        await typesenseClient.UpsertAliasAsync(settings.CollectionAlias, nextCollectionName, cancellationToken);

        var state = new SearchSyncState
        {
            Id = TypesenseSearchSyncStateStore.GetStateId(settings),
            SchemaVersion = SearchSchema.Version,
            ActiveCollection = nextCollectionName,
            LastSyncedAtUtc = DateTime.UtcNow,
            LastOrphanSweepAtUtc = DateTime.UtcNow
        };
        await stateStore.SaveStateAsync(state, cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousCollectionName) && !string.Equals(previousCollectionName, nextCollectionName, StringComparison.Ordinal))
        {
            await typesenseClient.DeleteCollectionAsync(previousCollectionName, ignoreIfMissing: true, cancellationToken);
        }
    }

    private async Task RunIncrementalSyncAsync(SearchSyncState state, string collectionName, CancellationToken cancellationToken)
    {
        var changedSince = (state.LastSyncedAtUtc ?? DateTime.UtcNow)
            .AddMinutes(-Math.Abs(settings.BatchWindowMinutes));

        var changedResources = await changedResourceEnumerator.GetChangedResources(changedSince, cancellationToken);
        if (changedResources.Count == 0)
        {
            state.LastSyncedAtUtc = DateTime.UtcNow;
            await stateStore.SaveStateAsync(state, cancellationToken);
            return;
        }

        var targets = await ExpandTargets(changedResources, collectionName, cancellationToken);
        var documents = await BuildDocuments(targets, cancellationToken);
        await ImportDocuments(collectionName, documents, cancellationToken);

        state.LastSyncedAtUtc = DateTime.UtcNow;
        await stateStore.SaveStateAsync(state, cancellationToken);
    }

    private async Task RunOrphanSweepAsync(SearchSyncState state, string collectionName, CancellationToken cancellationToken)
    {
        var lastSweep = state.LastOrphanSweepAtUtc ?? DateTime.MinValue;
        if (DateTime.UtcNow - lastSweep < TimeSpan.FromHours(settings.OrphanSweepIntervalHours))
        {
            return;
        }

        var indexedIds = await typesenseClient.ExportDocumentIdsAsync(collectionName, cancellationToken);
        var expectedIds = await changedResourceEnumerator.GetAllDocumentIds(cancellationToken);
        var expectedLookup = new HashSet<string>(expectedIds, StringComparer.Ordinal);

        foreach (var orphanId in indexedIds.Where(i => !expectedLookup.Contains(i)))
        {
            await typesenseClient.DeleteDocumentAsync(collectionName, orphanId, cancellationToken);
        }

        state.LastOrphanSweepAtUtc = DateTime.UtcNow;
        await stateStore.SaveStateAsync(state, cancellationToken);
    }

    private async Task<IReadOnlyList<SearchResourceTarget>> ExpandTargets(IReadOnlyList<SearchResourceTarget> changedResources,
        string collectionName, CancellationToken cancellationToken)
    {
        var targets = new Dictionary<string, SearchResourceTarget>();

        foreach (var target in changedResources)
        {
            targets[SearchDocumentId.Generate(target)] = target;

            if (target.ResourceType == SearchResourceType.Manifest) continue;

            var currentDocument = await searchDocumentBuilder.BuildAsync(target, cancellationToken);
            if (currentDocument == null) continue;

            var indexedDocument = await typesenseClient.GetDocumentAsync(collectionName, currentDocument.Id, cancellationToken);
            if (RequiresDescendantRefresh(indexedDocument, currentDocument))
            {
                var descendants = await changedResourceEnumerator.GetDescendants(target, cancellationToken);
                foreach (var descendant in descendants)
                {
                    targets[SearchDocumentId.Generate(descendant)] = descendant;
                }
            }
        }

        return targets.Values.ToList();
    }

    private static bool RequiresDescendantRefresh(JObject? indexedDocument, SearchDocument currentDocument)
    {
        if (indexedDocument == null) return true;

        return !string.Equals(indexedDocument["public_id"]?.Value<string>(), currentDocument.PublicId, StringComparison.Ordinal) ||
               !string.Equals(indexedDocument["full_path"]?.Value<string>(), currentDocument.FullPath, StringComparison.Ordinal);
    }

    private async Task<IReadOnlyList<SearchDocument>> BuildDocuments(IEnumerable<SearchResourceTarget> targets,
        CancellationToken cancellationToken)
    {
        var documents = new List<SearchDocument>();
        foreach (var target in targets)
        {
            var document = await searchDocumentBuilder.BuildAsync(target, cancellationToken);
            if (document != null)
            {
                documents.Add(document);
            }
        }

        return documents;
    }

    private async Task ImportDocuments(string collectionName, IReadOnlyList<SearchDocument> documents, CancellationToken cancellationToken)
    {
        foreach (var chunk in documents.Chunk(settings.ImportBatchSize))
        {
            var results = await typesenseClient.ImportDocumentsAsync(collectionName, chunk, cancellationToken);
            var failures = results.Where(r => !r.Success).ToList();
            if (failures.Count > 0)
            {
                throw new InvalidOperationException($"Typesense import failed: {string.Join(", ", failures.Select(f => f.Error))}");
            }
        }
    }
}
