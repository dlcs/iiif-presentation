namespace Services.Search;

public interface ISearchSyncStateStore
{
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);
    Task<SearchSyncState?> GetStateAsync(CancellationToken cancellationToken = default);
    Task SaveStateAsync(SearchSyncState state, CancellationToken cancellationToken = default);
}
