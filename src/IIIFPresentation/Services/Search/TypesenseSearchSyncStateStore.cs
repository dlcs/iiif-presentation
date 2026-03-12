namespace Services.Search;

public class TypesenseSearchSyncStateStore(
    ITypesenseClient typesenseClient,
    TypesenseSettings settings) : ISearchSyncStateStore
{
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await typesenseClient.CreateCollectionAsync(SearchSchema.GetStateCollectionSchema(SearchSchema.GetStateCollectionName(settings)),
            ignoreIfExists: true, cancellationToken);
    }

    public async Task<IReadOnlyList<SearchSyncState>> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        var documents = await typesenseClient.ExportDocumentsAsync(SearchSchema.GetStateCollectionName(settings),
            "id,customer_id,alias_name,schema_version,active_collection,last_synced_at,last_orphan_sweep_at", cancellationToken);

        return documents
            .Select(document => document.ToObject<SearchSyncState>())
            .OfType<SearchSyncState>()
            .ToList();
    }

    public async Task<SearchSyncState?> GetStateAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var document = await typesenseClient.GetDocumentAsync(SearchSchema.GetStateCollectionName(settings), GetStateId(customerId),
            cancellationToken);
        return document?.ToObject<SearchSyncState>();
    }

    public async Task SaveStateAsync(SearchSyncState state, CancellationToken cancellationToken = default)
    {
        var collectionName = SearchSchema.GetStateCollectionName(settings);
        var results = await typesenseClient.ImportDocumentsAsync(collectionName, [state], cancellationToken);
        if (results.All(r => r.Success))
        {
            return;
        }

        var failures = results.Where(r => !r.Success).ToList();
        if (RequiresSchemaRefresh(failures))
        {
            await typesenseClient.DeleteCollectionAsync(collectionName, ignoreIfMissing: true, cancellationToken);
            await typesenseClient.CreateCollectionAsync(SearchSchema.GetStateCollectionSchema(collectionName),
                cancellationToken: cancellationToken);
            results = await typesenseClient.ImportDocumentsAsync(collectionName, [state], cancellationToken);
            failures = results.Where(r => !r.Success).ToList();
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException($"Unable to persist search sync state: {string.Join(", ", failures.Select(r => r.Error))}");
        }
    }

    public async Task DeleteStateAsync(int customerId, CancellationToken cancellationToken = default)
    {
        await typesenseClient.DeleteDocumentAsync(SearchSchema.GetStateCollectionName(settings), GetStateId(customerId),
            cancellationToken);
    }

    public static string GetStateId(int customerId) => SearchSchema.GetStateId(customerId);

    private static bool RequiresSchemaRefresh(IReadOnlyList<TypesenseImportResult> failures) =>
        failures.Any(failure =>
            failure.Error?.Contains("customer_id", StringComparison.OrdinalIgnoreCase) == true ||
            failure.Error?.Contains("alias_name", StringComparison.OrdinalIgnoreCase) == true);
}
