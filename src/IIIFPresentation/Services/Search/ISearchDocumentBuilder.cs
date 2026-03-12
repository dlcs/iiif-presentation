namespace Services.Search;

public interface ISearchDocumentBuilder
{
    Task<SearchDocument?> BuildAsync(SearchResourceTarget target, CancellationToken cancellationToken = default);
}
