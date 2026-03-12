namespace Services.Search;

public interface IChangedResourceEnumerator
{
    IAsyncEnumerable<IReadOnlyList<SearchResourceTarget>> GetAllResources(int batchSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResourceTarget>> GetChangedResources(DateTime changedSince, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResourceTarget>> GetDescendants(SearchResourceTarget collectionTarget, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> GetAllDocumentIds(CancellationToken cancellationToken = default);
}
