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

        var discoveredCustomerIds = await changedResourceEnumerator.GetCustomerIdsAsync(cancellationToken);
        var includedCustomerIds = discoveredCustomerIds
            .Where(settings.IsCustomerIncluded)
            .OrderBy(customerId => customerId)
            .ToArray();
        var trackedStates = await stateStore.GetAllStatesAsync(cancellationToken);
        var trackedStateLookup = trackedStates
            .GroupBy(state => state.CustomerId)
            .ToDictionary(group => group.Key, group => group.Last());

        foreach (var customerId in includedCustomerIds)
        {
            var aliasName = SearchSchema.GetAliasName(settings, customerId);
            trackedStateLookup.TryGetValue(customerId, out var state);

            var alias = await typesenseClient.GetAliasAsync(aliasName, cancellationToken);
            if (RequiresBootstrap(aliasName, alias, state))
            {
                await BootstrapAsync(customerId, aliasName, state?.ActiveCollection, cancellationToken);
                continue;
            }

            if (state?.ActiveCollection == null) continue;

            await RunIncrementalSyncAsync(customerId, state, state.ActiveCollection, cancellationToken);
            await RunOrphanSweepAsync(customerId, state, state.ActiveCollection, cancellationToken);
        }

        var includedCustomerLookup = includedCustomerIds.ToHashSet();
        var staleStates = trackedStates
            .GroupBy(state => state.CustomerId)
            .Select(group => group.Last())
            .Where(state => !includedCustomerLookup.Contains(state.CustomerId))
            .ToList();

        foreach (var staleState in staleStates)
        {
            await CleanupCustomerAsync(staleState, cancellationToken);
        }
    }

    public async Task TryDeleteResourceDocumentAsync(IHierarchyResource resource, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured || !settings.IsCustomerIncluded(resource.CustomerId)) return;

        var resourceType = resource switch
        {
            Collection collection when collection.IsStorageCollection => SearchResourceType.StorageCollection,
            Collection => SearchResourceType.IiifCollection,
            Manifest => SearchResourceType.Manifest,
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null)
        };

        var aliasName = SearchSchema.GetAliasName(settings, resource.CustomerId);
        var documentId = SearchDocumentId.Generate(resource.CustomerId, resourceType, resource.Id);

        try
        {
            await typesenseClient.DeleteDocumentAsync(aliasName, documentId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to delete search document {DocumentId}", documentId);
        }
    }

    private static bool RequiresBootstrap(string aliasName, TypesenseAlias? alias, SearchSyncState? state)
    {
        if (alias?.CollectionName == null || state == null)
        {
            return true;
        }

        return state.CustomerId <= 0 ||
               !string.Equals(state.AliasName, aliasName, StringComparison.Ordinal) ||
               state.SchemaVersion != SearchSchema.Version ||
               string.IsNullOrWhiteSpace(state.ActiveCollection) ||
               !string.Equals(state.ActiveCollection, alias.CollectionName, StringComparison.Ordinal);
    }

    private async Task BootstrapAsync(int customerId, string aliasName, string? previousCollectionName,
        CancellationToken cancellationToken)
    {
        var nextCollectionName = SearchSchema.GenerateCollectionName(settings, customerId);
        logger.LogInformation("Bootstrapping Typesense collection {CollectionName} for customer {CustomerId}",
            nextCollectionName, customerId);

        await typesenseClient.CreateCollectionAsync(SearchSchema.GetSearchCollectionSchema(nextCollectionName),
            cancellationToken: cancellationToken);

        await foreach (var batch in changedResourceEnumerator.GetAllResources(customerId, settings.BootstrapBatchSize, cancellationToken))
        {
            var documents = await BuildDocuments(batch, cancellationToken);
            await ImportDocuments(nextCollectionName, documents, cancellationToken);
        }

        await typesenseClient.UpsertAliasAsync(aliasName, nextCollectionName, cancellationToken);

        var state = new SearchSyncState
        {
            Id = TypesenseSearchSyncStateStore.GetStateId(customerId),
            CustomerId = customerId,
            AliasName = aliasName,
            SchemaVersion = SearchSchema.Version,
            ActiveCollection = nextCollectionName,
            LastSyncedAtUtc = DateTime.UtcNow,
            LastOrphanSweepAtUtc = DateTime.UtcNow
        };
        await stateStore.SaveStateAsync(state, cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousCollectionName) &&
            !string.Equals(previousCollectionName, nextCollectionName, StringComparison.Ordinal))
        {
            await typesenseClient.DeleteCollectionAsync(previousCollectionName, ignoreIfMissing: true, cancellationToken);
        }
    }

    private async Task RunIncrementalSyncAsync(int customerId, SearchSyncState state, string collectionName,
        CancellationToken cancellationToken)
    {
        var changedSince = (state.LastSyncedAtUtc ?? DateTime.UtcNow)
            .AddMinutes(-Math.Abs(settings.BatchWindowMinutes));

        var changedResources = await changedResourceEnumerator.GetChangedResources(customerId, changedSince, cancellationToken);
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

    private async Task RunOrphanSweepAsync(int customerId, SearchSyncState state, string collectionName,
        CancellationToken cancellationToken)
    {
        var lastSweep = state.LastOrphanSweepAtUtc ?? DateTime.MinValue;
        if (DateTime.UtcNow - lastSweep < TimeSpan.FromHours(settings.OrphanSweepIntervalHours))
        {
            return;
        }

        var indexedIds = await typesenseClient.ExportDocumentIdsAsync(collectionName, cancellationToken);
        var expectedIds = await changedResourceEnumerator.GetAllDocumentIds(customerId, cancellationToken);
        var expectedLookup = new HashSet<string>(expectedIds, StringComparer.Ordinal);

        foreach (var orphanId in indexedIds.Where(indexedId => !expectedLookup.Contains(indexedId)))
        {
            await typesenseClient.DeleteDocumentAsync(collectionName, orphanId, cancellationToken);
        }

        state.LastOrphanSweepAtUtc = DateTime.UtcNow;
        await stateStore.SaveStateAsync(state, cancellationToken);
    }

    private async Task CleanupCustomerAsync(SearchSyncState state, CancellationToken cancellationToken)
    {
        var aliasNames = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(state.AliasName))
        {
            aliasNames.Add(state.AliasName);
        }
        else
        {
            aliasNames.Add(SearchSchema.GetAliasName(settings, state.CustomerId));
        }

        var expectedStateId = SearchSchema.GetStateId(state.CustomerId);
        if (!string.IsNullOrWhiteSpace(state.Id) && !string.Equals(state.Id, expectedStateId, StringComparison.Ordinal))
        {
            aliasNames.Add(state.Id);
        }

        foreach (var aliasName in aliasNames)
        {
            await typesenseClient.DeleteAliasAsync(aliasName, ignoreIfMissing: true, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(state.ActiveCollection))
        {
            await typesenseClient.DeleteCollectionAsync(state.ActiveCollection, ignoreIfMissing: true, cancellationToken);
        }

        if (state.CustomerId > 0)
        {
            await stateStore.DeleteStateAsync(state.CustomerId, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(state.Id))
        {
            await typesenseClient.DeleteDocumentAsync(SearchSchema.GetStateCollectionName(settings), state.Id, cancellationToken);
        }
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
