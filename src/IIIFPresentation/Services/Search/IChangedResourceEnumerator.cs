namespace Services.Search;

public interface IChangedResourceEnumerator
{
    Task<IReadOnlyList<int>> GetCustomerIdsAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<IReadOnlyList<SearchResourceTarget>> GetAllResources(int customerId, int batchSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResourceTarget>> GetChangedResources(int customerId, DateTime changedSince,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResourceTarget>> GetDescendants(SearchResourceTarget collectionTarget, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> GetAllDocumentIds(int customerId, CancellationToken cancellationToken = default);
}
