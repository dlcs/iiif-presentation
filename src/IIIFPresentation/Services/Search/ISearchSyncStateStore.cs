namespace Services.Search;

public interface ISearchSyncStateStore
{
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchSyncState>> GetAllStatesAsync(CancellationToken cancellationToken = default);
    Task<SearchSyncState?> GetStateAsync(int customerId, CancellationToken cancellationToken = default);
    Task SaveStateAsync(SearchSyncState state, CancellationToken cancellationToken = default);
    Task DeleteStateAsync(int customerId, CancellationToken cancellationToken = default);
}
