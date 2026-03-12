using Models.Database.Collections;

namespace Services.Search;

public class NoOpSearchSyncService : ISearchSyncService
{
    public Task RunOnce(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task TryDeleteResourceDocumentAsync(IHierarchyResource resource, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
