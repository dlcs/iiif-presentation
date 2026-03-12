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

    public async Task<SearchSyncState?> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var document = await typesenseClient.GetDocumentAsync(SearchSchema.GetStateCollectionName(settings), GetStateId(),
            cancellationToken);
        return document?.ToObject<SearchSyncState>();
    }

    public async Task SaveStateAsync(SearchSyncState state, CancellationToken cancellationToken = default)
    {
        var results = await typesenseClient.ImportDocumentsAsync(SearchSchema.GetStateCollectionName(settings), [state],
            cancellationToken);

        if (results.Any(r => !r.Success))
        {
            throw new InvalidOperationException($"Unable to persist search sync state: {string.Join(", ", results.Where(r => !r.Success).Select(r => r.Error))}");
        }
    }

    public static string GetStateId(TypesenseSettings settings) => settings.CollectionAlias;

    private string GetStateId() => GetStateId(settings);
}
