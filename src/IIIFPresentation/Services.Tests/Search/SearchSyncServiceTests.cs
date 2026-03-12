using Microsoft.Extensions.Logging.Abstractions;
using Models.Database.Collections;
using Newtonsoft.Json.Linq;
using Services.Search;

namespace Services.Tests.Search;

public class SearchSyncServiceTests
{
    [Fact]
    public async Task RunOnce_Bootstraps_WhenAliasMissing()
    {
        var settings = GetSettings();
        var client = new FakeTypesenseClient();
        var stateStore = new FakeStateStore();
        var enumerator = new FakeChangedResourceEnumerator
        {
            AllResources =
            [
                [new SearchResourceTarget(1, "manifest-1", SearchResourceType.Manifest)]
            ]
        };
        var builder = new FakeSearchDocumentBuilder();
        builder.Documents[SearchDocumentId.Generate(1, SearchResourceType.Manifest, "manifest-1")] = new SearchDocument
        {
            Id = SearchDocumentId.Generate(1, SearchResourceType.Manifest, "manifest-1"),
            CustomerId = 1,
            ResourceType = "manifest",
            FlatId = "manifest-1",
            PublicId = "https://example.org/presentation/manifest-1",
            ApiId = "https://example.org/1/manifests/manifest-1",
            Slug = "manifest-1",
            ModifiedTimestamp = 1
        };

        var sut = new SearchSyncService(settings, client, stateStore, enumerator, builder,
            NullLogger<SearchSyncService>.Instance);

        await sut.RunOnce();

        client.CreatedCollections.Should().ContainSingle(c => c.StartsWith($"{settings.CollectionAlias}_v{SearchSchema.Version}_"));
        client.ImportedBatches.Should().ContainSingle();
        client.UpsertedAliases.Should().ContainSingle()
            .Which.aliasName.Should().Be(settings.CollectionAlias);
        stateStore.State.Should().NotBeNull();
        stateStore.State!.ActiveCollection.Should().Be(client.CreatedCollections.Single());
        stateStore.State.SchemaVersion.Should().Be(SearchSchema.Version);
    }

    [Fact]
    public async Task RunOnce_ReindexesDescendants_WhenCollectionPathChanges()
    {
        var settings = GetSettings();
        var collectionTarget = new SearchResourceTarget(1, "collection-1", SearchResourceType.StorageCollection);
        var descendantTarget = new SearchResourceTarget(1, "manifest-2", SearchResourceType.Manifest);
        var currentCollectionDocument = new SearchDocument
        {
            Id = SearchDocumentId.Generate(collectionTarget),
            CustomerId = 1,
            ResourceType = "storage_collection",
            FlatId = "collection-1",
            PublicId = "https://example.org/presentation/new-path",
            ApiId = "https://example.org/1/collections/collection-1",
            Slug = "new-path",
            FullPath = "new-path",
            ModifiedTimestamp = 1
        };
        var currentDescendantDocument = new SearchDocument
        {
            Id = SearchDocumentId.Generate(descendantTarget),
            CustomerId = 1,
            ResourceType = "manifest",
            FlatId = "manifest-2",
            PublicId = "https://example.org/presentation/new-path/manifest-2",
            ApiId = "https://example.org/1/manifests/manifest-2",
            Slug = "manifest-2",
            FullPath = "new-path/manifest-2",
            ModifiedTimestamp = 2
        };

        var client = new FakeTypesenseClient
        {
            Alias = new TypesenseAlias
            {
                Name = settings.CollectionAlias,
                CollectionName = "iiif_presentation_current"
            }
        };
        client.Documents[(client.Alias.CollectionName!, currentCollectionDocument.Id)] = new JObject
        {
            ["id"] = currentCollectionDocument.Id,
            ["public_id"] = "https://example.org/presentation/old-path",
            ["full_path"] = "old-path"
        };

        var stateStore = new FakeStateStore
        {
            State = new SearchSyncState
            {
                Id = settings.CollectionAlias,
                ActiveCollection = client.Alias.CollectionName,
                SchemaVersion = SearchSchema.Version,
                LastSyncedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                LastOrphanSweepAtUtc = DateTime.UtcNow
            }
        };
        var enumerator = new FakeChangedResourceEnumerator
        {
            ChangedResources = [collectionTarget],
            Descendants = { [SearchDocumentId.Generate(collectionTarget)] = [descendantTarget] }
        };
        var builder = new FakeSearchDocumentBuilder();
        builder.Documents[currentCollectionDocument.Id] = currentCollectionDocument;
        builder.Documents[currentDescendantDocument.Id] = currentDescendantDocument;

        var sut = new SearchSyncService(settings, client, stateStore, enumerator, builder,
            NullLogger<SearchSyncService>.Instance);

        await sut.RunOnce();

        client.ImportedBatches.Should().ContainSingle();
        client.ImportedBatches[0].documents.Should().HaveCount(2);
        client.ImportedBatches[0].documents.Select(d => d.Id).Should().BeEquivalentTo(
            new[] { currentCollectionDocument.Id, currentDescendantDocument.Id });
    }

    [Fact]
    public async Task RunOnce_RemovesOrphans_WhenSweepIsDue()
    {
        var settings = GetSettings();
        var client = new FakeTypesenseClient
        {
            Alias = new TypesenseAlias
            {
                Name = settings.CollectionAlias,
                CollectionName = "iiif_presentation_current"
            },
            ExportedIds = ["1:manifest:keep", "1:manifest:remove"]
        };
        var stateStore = new FakeStateStore
        {
            State = new SearchSyncState
            {
                Id = settings.CollectionAlias,
                ActiveCollection = client.Alias.CollectionName,
                SchemaVersion = SearchSchema.Version,
                LastSyncedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                LastOrphanSweepAtUtc = DateTime.UtcNow.AddHours(-48)
            }
        };
        var enumerator = new FakeChangedResourceEnumerator
        {
            AllDocumentIds = ["1:manifest:keep"]
        };
        var sut = new SearchSyncService(settings, client, stateStore, enumerator, new FakeSearchDocumentBuilder(),
            NullLogger<SearchSyncService>.Instance);

        await sut.RunOnce();

        client.DeletedDocuments.Should().ContainSingle(d => d.documentId == "1:manifest:remove");
        stateStore.State!.LastOrphanSweepAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
    }

    private static TypesenseSettings GetSettings() => new()
    {
        BaseUrl = "https://typesense.example",
        ApiKey = "secret",
        CollectionAlias = "iiif_presentation",
        ImportBatchSize = 100,
        BootstrapBatchSize = 100,
        BatchWindowMinutes = 5,
        OrphanSweepIntervalHours = 24
    };

    private sealed class FakeTypesenseClient : ITypesenseClient
    {
        public TypesenseAlias? Alias { get; set; }
        public List<string> CreatedCollections { get; } = [];
        public List<(string aliasName, string collectionName)> UpsertedAliases { get; } = [];
        public List<(string collectionName, List<SearchDocument> documents)> ImportedBatches { get; } = [];
        public Dictionary<(string collectionName, string documentId), JObject> Documents { get; } = [];
        public IReadOnlyCollection<string> ExportedIds { get; set; } = [];
        public List<(string collectionName, string documentId)> DeletedDocuments { get; } = [];

        public Task<TypesenseAlias?> GetAliasAsync(string aliasName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Alias);

        public Task UpsertAliasAsync(string aliasName, string collectionName, CancellationToken cancellationToken = default)
        {
            UpsertedAliases.Add((aliasName, collectionName));
            Alias = new TypesenseAlias { Name = aliasName, CollectionName = collectionName };
            return Task.CompletedTask;
        }

        public Task CreateCollectionAsync(object schema, bool ignoreIfExists = false, CancellationToken cancellationToken = default)
        {
            var schemaObject = JObject.FromObject(schema);
            CreatedCollections.Add(schemaObject["name"]!.Value<string>()!);
            return Task.CompletedTask;
        }

        public Task DeleteCollectionAsync(string collectionName, bool ignoreIfMissing = false, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<TypesenseImportResult>> ImportDocumentsAsync(string collectionName, IEnumerable<object> documents,
            CancellationToken cancellationToken = default)
        {
            ImportedBatches.Add((collectionName, documents.Cast<SearchDocument>().ToList()));
            return Task.FromResult<IReadOnlyList<TypesenseImportResult>>([new TypesenseImportResult { Success = true }]);
        }

        public Task<JObject?> GetDocumentAsync(string collectionName, string documentId, CancellationToken cancellationToken = default)
        {
            Documents.TryGetValue((collectionName, documentId), out var document);
            return Task.FromResult(document);
        }

        public Task<bool> DeleteDocumentAsync(string collectionName, string documentId, CancellationToken cancellationToken = default)
        {
            DeletedDocuments.Add((collectionName, documentId));
            return Task.FromResult(true);
        }

        public Task<IReadOnlyCollection<string>> ExportDocumentIdsAsync(string collectionName, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExportedIds);
    }

    private sealed class FakeStateStore : ISearchSyncStateStore
    {
        public bool EnsureCreatedCalled { get; private set; }
        public SearchSyncState? State { get; set; }

        public Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
        {
            EnsureCreatedCalled = true;
            return Task.CompletedTask;
        }

        public Task<SearchSyncState?> GetStateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public Task SaveStateAsync(SearchSyncState state, CancellationToken cancellationToken = default)
        {
            State = state;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeChangedResourceEnumerator : IChangedResourceEnumerator
    {
        public IReadOnlyList<IReadOnlyList<SearchResourceTarget>> AllResources { get; init; } = [];
        public IReadOnlyList<SearchResourceTarget> ChangedResources { get; init; } = [];
        public Dictionary<string, IReadOnlyList<SearchResourceTarget>> Descendants { get; } = [];
        public IReadOnlyCollection<string> AllDocumentIds { get; init; } = [];

        public async IAsyncEnumerable<IReadOnlyList<SearchResourceTarget>> GetAllResources(int batchSize,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var batch in AllResources)
            {
                yield return batch;
                await Task.CompletedTask;
            }
        }

        public Task<IReadOnlyList<SearchResourceTarget>> GetChangedResources(DateTime changedSince, CancellationToken cancellationToken = default) =>
            Task.FromResult(ChangedResources);

        public Task<IReadOnlyList<SearchResourceTarget>> GetDescendants(SearchResourceTarget collectionTarget,
            CancellationToken cancellationToken = default)
        {
            Descendants.TryGetValue(SearchDocumentId.Generate(collectionTarget), out var descendants);
            return Task.FromResult<IReadOnlyList<SearchResourceTarget>>(descendants ?? []);
        }

        public Task<IReadOnlyCollection<string>> GetAllDocumentIds(CancellationToken cancellationToken = default) =>
            Task.FromResult(AllDocumentIds);
    }

    private sealed class FakeSearchDocumentBuilder : ISearchDocumentBuilder
    {
        public Dictionary<string, SearchDocument> Documents { get; } = [];

        public Task<SearchDocument?> BuildAsync(SearchResourceTarget target, CancellationToken cancellationToken = default)
        {
            Documents.TryGetValue(SearchDocumentId.Generate(target), out var document);
            return Task.FromResult(document);
        }
    }
}
