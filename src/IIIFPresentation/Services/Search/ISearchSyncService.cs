using Models.Database.Collections;

namespace Services.Search;

public interface ISearchSyncService
{
    Task RunOnce(CancellationToken cancellationToken = default);
    Task TryDeleteResourceDocumentAsync(IHierarchyResource resource, CancellationToken cancellationToken = default);
}
