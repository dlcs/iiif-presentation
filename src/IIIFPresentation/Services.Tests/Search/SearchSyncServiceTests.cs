using Microsoft.Extensions.Logging.Abstractions;
using Models.Database.Collections;
using Newtonsoft.Json.Linq;
using Services.Search;

namespace Services.Tests.Search;

public class SearchSyncServiceTests
{
    [Fact]
    public async Task RunOnce_BootstrapsOnlyIncludedCustomers()
    {
        var settings = GetSettings();
        settings.WhitelistCustomerIds = [1];

        var client = new FakeTypesenseClient();
        var stateStore = new FakeStateStore();
        var enumerator = new FakeChangedResourceEnumerator
        {
            CustomerIds = [1, 2]
        };
        enumerator.AllResourcesByCustomer[1] =
        [
            [new SearchResourceTarget(1, "manifest-1", SearchResourceType.Manifest)]
        ];
        enumerator.AllResourcesByCustomer[2] =
        [
            [new SearchResourceTarget(2, "manifest-2", SearchResourceType.Manifest)]
        ];

        var builder = new FakeSearchDocumentBuilder();
        builder.Documents[SearchDocumentId.Generate(1, SearchResourceType.Manifest, "manifest-1")] = CreateDocument(1, "manifest-1");
        builder.Documents[SearchDocumentId.Generate(2, SearchResourceType.Manifest, "manifest-2")] = CreateDocument(2, "manifest-2");

        var sut = CreateSut(settings, client, stateStore, enumerator, builder);

        await sut.RunOnce();

        client.CreatedCollections.Should().ContainSingle(collectionName =>
            collectionName.StartsWith($"{settings.CollectionPrefix}_customer_1_v{SearchSchema.Version}_"));
        client.UpsertedAliases.Should().ContainSingle()
            .Which.aliasName.Should().Be(SearchSchema.GetAliasName(settings, 1));
        stateStore.States.Should().ContainKey(1);
        stateStore.States.Should().NotContainKey(2);
    }

    [Fact]
    public async Task RunOnce_ReindexesDescendants_WhenCollectionPathChanges()
    {
        var settings = GetSettings();
        var collectionTarget = new SearchResourceTarget(1, "collection-1", SearchResourceType.StorageCollection);
        var descendantTarget = new SearchResourceTarget(1, "manifest-2", SearchResourceType.Manifest);
        var aliasName = SearchSchema.GetAliasName(settings, 1);
        var collectionName = $"{aliasName}_current";

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
        var currentDescendantDocument = CreateDocument(1, "manifest-2", "new-path/manifest-2");

        var client = new FakeTypesenseClient();
        client.Aliases[aliasName] = new TypesenseAlias { Name = aliasName, CollectionName = collectionName };
        client.Documents[(collectionName, currentCollectionDocument.Id)] = new JObject
        {
            ["id"] = currentCollectionDocument.Id,
            ["public_id"] = "https://example.org/presentation/old-path",
            ["full_path"] = "old-path"
        };

        var stateStore = new FakeStateStore();
        stateStore.States[1] = new SearchSyncState
        {
            Id = SearchSchema.GetStateId(1),
            CustomerId = 1,
            AliasName = aliasName,
            ActiveCollection = collectionName,
            SchemaVersion = SearchSchema.Version,
            LastSyncedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            LastOrphanSweepAtUtc = DateTime.UtcNow
        };

        var enumerator = new FakeChangedResourceEnumerator
        {
            CustomerIds = [1]
        };
        enumerator.ChangedResourcesByCustomer[1] = [collectionTarget];
        enumerator.Descendants[SearchDocumentId.Generate(collectionTarget)] = [descendantTarget];

        var builder = new FakeSearchDocumentBuilder();
        builder.Documents[currentCollectionDocument.Id] = currentCollectionDocument;
        builder.Documents[currentDescendantDocument.Id] = currentDescendantDocument;

        var sut = CreateSut(settings, client, stateStore, enumerator, builder);

        await sut.RunOnce();

        client.ImportedBatches.Should().ContainSingle();
        client.ImportedBatches[0].collectionName.Should().Be(collectionName);
        client.ImportedBatches[0].documents.Select(document => document.Id).Should().BeEquivalentTo(
            new[] { currentCollectionDocument.Id, currentDescendantDocument.Id });
    }

    [Fact]
    public async Task RunOnce_RemovesOrphans_PerCustomerCollection()
    {
        var settings = GetSettings();
        var aliasName = SearchSchema.GetAliasName(settings, 1);
        var collectionName = $"{aliasName}_current";

        var client = new FakeTypesenseClient();
        client.Aliases[aliasName] = new TypesenseAlias { Name = aliasName, CollectionName = collectionName };
        client.ExportedIdsByCollection[collectionName] = ["1:manifest:keep", "1:manifest:remove"];

        var stateStore = new FakeStateStore();
        stateStore.States[1] = new SearchSyncState
        {
            Id = SearchSchema.GetStateId(1),
            CustomerId = 1,
            AliasName = aliasName,
            ActiveCollection = collectionName,
            SchemaVersion = SearchSchema.Version,
            LastSyncedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            LastOrphanSweepAtUtc = DateTime.UtcNow.AddHours(-48)
        };

        var enumerator = new FakeChangedResourceEnumerator
        {
            CustomerIds = [1]
        };
        enumerator.AllDocumentIdsByCustomer[1] = ["1:manifest:keep"];

        var sut = CreateSut(settings, client, stateStore, enumerator, new FakeSearchDocumentBuilder());

        await sut.RunOnce();

        client.DeletedDocuments.Should().ContainSingle(document => document.documentId == "1:manifest:remove");
        stateStore.States[1].LastOrphanSweepAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task RunOnce_RemovesExcludedTrackedCustomers()
    {
        var settings = GetSettings();
        settings.BlacklistCustomerIds = [2];
        var excludedAlias = SearchSchema.GetAliasName(settings, 2);
        var excludedCollection = $"{excludedAlias}_current";

        var client = new FakeTypesenseClient();
        client.Aliases[excludedAlias] = new TypesenseAlias { Name = excludedAlias, CollectionName = excludedCollection };

        var stateStore = new FakeStateStore();
        stateStore.States[2] = new SearchSyncState
        {
            Id = SearchSchema.GetStateId(2),
            CustomerId = 2,
            AliasName = excludedAlias,
            ActiveCollection = excludedCollection,
            SchemaVersion = SearchSchema.Version,
            LastSyncedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };

        var enumerator = new FakeChangedResourceEnumerator
        {
            CustomerIds = [2]
        };

        var sut = CreateSut(settings, client, stateStore, enumerator, new FakeSearchDocumentBuilder());

        await sut.RunOnce();

        client.DeletedAliases.Should().Contain(excludedAlias);
        client.DeletedCollections.Should().Contain(excludedCollection);
        stateStore.States.Should().NotContainKey(2);
    }

    [Fact]
    public async Task RunOnce_RemovesTrackedCustomers_WithNoRemainingResources()
    {
        var settings = GetSettings();
        var aliasName = SearchSchema.GetAliasName(settings, 4);
        var collectionName = $"{aliasName}_current";

        var client = new FakeTypesenseClient();
        client.Aliases[aliasName] = new TypesenseAlias { Name = aliasName, CollectionName = collectionName };

        var stateStore = new FakeStateStore();
        stateStore.States[4] = new SearchSyncState
        {
            Id = SearchSchema.GetStateId(4),
            CustomerId = 4,
            AliasName = aliasName,
            ActiveCollection = collectionName,
            SchemaVersion = SearchSchema.Version
        };

        var sut = CreateSut(settings, client, stateStore, new FakeChangedResourceEnumerator(), new FakeSearchDocumentBuilder());

        await sut.RunOnce();

        client.DeletedAliases.Should().Contain(aliasName);
        client.DeletedCollections.Should().Contain(collectionName);
        stateStore.States.Should().NotContainKey(4);
    }

    [Fact]
    public async Task RunOnce_CleansLegacyGlobalState_WhenMigratingToCustomerScopedCollections()
    {
        var settings = GetSettings();
        var client = new FakeTypesenseClient();
        var stateStore = new FakeStateStore();
        stateStore.States[0] = new SearchSyncState
        {
            Id = "iiif_presentation",
            CustomerId = 0,
            AliasName = string.Empty,
            ActiveCollection = "iiif_presentation_v1_legacy",
            SchemaVersion = 1
        };

        var enumerator = new FakeChangedResourceEnumerator
        {
            CustomerIds = [1]
        };
        enumerator.AllResourcesByCustomer[1] =
        [
            [new SearchResourceTarget(1, "manifest-1", SearchResourceType.Manifest)]
        ];

        var builder = new FakeSearchDocumentBuilder();
        builder.Documents[SearchDocumentId.Generate(1, SearchResourceType.Manifest, "manifest-1")] = CreateDocument(1, "manifest-1");

        var sut = CreateSut(settings, client, stateStore, enumerator, builder);

        await sut.RunOnce();

        client.DeletedAliases.Should().Contain("iiif_presentation");
        client.DeletedCollections.Should().Contain("iiif_presentation_v1_legacy");
    }

    [Fact]
    public async Task TryDeleteResourceDocumentAsync_UsesCustomerAlias()
    {
        var settings = GetSettings();
        var client = new FakeTypesenseClient();
        var sut = CreateSut(settings, client, new FakeStateStore(), new FakeChangedResourceEnumerator(),
            new FakeSearchDocumentBuilder());

        await sut.TryDeleteResourceDocumentAsync(new Manifest { Id = "manifest-1", CustomerId = 5 });

        client.DeletedDocuments.Should().ContainSingle(document =>
            document.collectionName == SearchSchema.GetAliasName(settings, 5) &&
            document.documentId == SearchDocumentId.Generate(5, SearchResourceType.Manifest, "manifest-1"));
    }

    [Fact]
    public async Task TryDeleteResourceDocumentAsync_SkipsExcludedCustomers()
    {
        var settings = GetSettings();
        settings.BlacklistCustomerIds = [5];
        var client = new FakeTypesenseClient();
        var sut = CreateSut(settings, client, new FakeStateStore(), new FakeChangedResourceEnumerator(),
            new FakeSearchDocumentBuilder());

        await sut.TryDeleteResourceDocumentAsync(new Manifest { Id = "manifest-1", CustomerId = 5 });

        client.DeletedDocuments.Should().BeEmpty();
    }

    private static SearchSyncService CreateSut(TypesenseSettings settings, FakeTypesenseClient client, FakeStateStore stateStore,
        FakeChangedResourceEnumerator enumerator, FakeSearchDocumentBuilder builder) =>
        new(settings, client, stateStore, enumerator, builder, NullLogger<SearchSyncService>.Instance);

    private static TypesenseSettings GetSettings() => new()
    {
        BaseUrl = "https://typesense.example",
        ApiKey = "secret",
        CollectionPrefix = "iiif_presentation",
        ImportBatchSize = 100,
        BootstrapBatchSize = 100,
        BatchWindowMinutes = 5,
        OrphanSweepIntervalHours = 24
    };

    private static SearchDocument CreateDocument(int customerId, string flatId, string? fullPath = null) => new()
    {
        Id = SearchDocumentId.Generate(customerId, SearchResourceType.Manifest, flatId),
        CustomerId = customerId,
        ResourceType = "manifest",
        FlatId = flatId,
        PublicId = $"https://example.org/presentation/{fullPath ?? flatId}",
        ApiId = $"https://example.org/{customerId}/manifests/{flatId}",
        Slug = flatId,
        FullPath = fullPath ?? flatId,
        ModifiedTimestamp = 1
    };

    private sealed class FakeTypesenseClient : ITypesenseClient
    {
        public Dictionary<string, TypesenseAlias> Aliases { get; } = [];
        public List<string> CreatedCollections { get; } = [];
        public List<(string aliasName, string collectionName)> UpsertedAliases { get; } = [];
        public List<string> DeletedAliases { get; } = [];
        public List<string> DeletedCollections { get; } = [];
        public List<(string collectionName, List<SearchDocument> documents)> ImportedBatches { get; } = [];
        public Dictionary<(string collectionName, string documentId), JObject> Documents { get; } = [];
        public Dictionary<string, IReadOnlyCollection<string>> ExportedIdsByCollection { get; } = [];
        public List<(string collectionName, string documentId)> DeletedDocuments { get; } = [];

        public Task<TypesenseAlias?> GetAliasAsync(string aliasName, CancellationToken cancellationToken = default) =>
            Task.FromResult<TypesenseAlias?>(Aliases.GetValueOrDefault(aliasName));

        public Task UpsertAliasAsync(string aliasName, string collectionName, CancellationToken cancellationToken = default)
        {
            UpsertedAliases.Add((aliasName, collectionName));
            Aliases[aliasName] = new TypesenseAlias { Name = aliasName, CollectionName = collectionName };
            return Task.CompletedTask;
        }

        public Task DeleteAliasAsync(string aliasName, bool ignoreIfMissing = false, CancellationToken cancellationToken = default)
        {
            DeletedAliases.Add(aliasName);
            Aliases.Remove(aliasName);
            return Task.CompletedTask;
        }

        public Task CreateCollectionAsync(object schema, bool ignoreIfExists = false, CancellationToken cancellationToken = default)
        {
            var schemaObject = JObject.FromObject(schema);
            CreatedCollections.Add(schemaObject["name"]!.Value<string>()!);
            return Task.CompletedTask;
        }

        public Task DeleteCollectionAsync(string collectionName, bool ignoreIfMissing = false, CancellationToken cancellationToken = default)
        {
            DeletedCollections.Add(collectionName);
            return Task.CompletedTask;
        }

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

        public Task<IReadOnlyCollection<JObject>> ExportDocumentsAsync(string collectionName, string? includeFields = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<JObject>>([]);

        public Task<IReadOnlyCollection<string>> ExportDocumentIdsAsync(string collectionName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>(ExportedIdsByCollection.GetValueOrDefault(collectionName, Array.Empty<string>()));
    }

    private sealed class FakeStateStore : ISearchSyncStateStore
    {
        public Dictionary<int, SearchSyncState> States { get; } = [];

        public Task EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<SearchSyncState>> GetAllStatesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchSyncState>>(States.Values.ToList());

        public Task<SearchSyncState?> GetStateAsync(int customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SearchSyncState?>(States.GetValueOrDefault(customerId));

        public Task SaveStateAsync(SearchSyncState state, CancellationToken cancellationToken = default)
        {
            States[state.CustomerId] = state;
            return Task.CompletedTask;
        }

        public Task DeleteStateAsync(int customerId, CancellationToken cancellationToken = default)
        {
            States.Remove(customerId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeChangedResourceEnumerator : IChangedResourceEnumerator
    {
        public IReadOnlyList<int> CustomerIds { get; init; } = [];
        public Dictionary<int, IReadOnlyList<IReadOnlyList<SearchResourceTarget>>> AllResourcesByCustomer { get; } = [];
        public Dictionary<int, IReadOnlyList<SearchResourceTarget>> ChangedResourcesByCustomer { get; } = [];
        public Dictionary<string, IReadOnlyList<SearchResourceTarget>> Descendants { get; } = [];
        public Dictionary<int, IReadOnlyCollection<string>> AllDocumentIdsByCustomer { get; } = [];

        public Task<IReadOnlyList<int>> GetCustomerIdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<int>>(CustomerIds);

        public async IAsyncEnumerable<IReadOnlyList<SearchResourceTarget>> GetAllResources(int customerId, int batchSize,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var batch in AllResourcesByCustomer.GetValueOrDefault(customerId, []))
            {
                yield return batch;
                await Task.Yield();
            }
        }

        public Task<IReadOnlyList<SearchResourceTarget>> GetChangedResources(int customerId, DateTime changedSince,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchResourceTarget>>(ChangedResourcesByCustomer.GetValueOrDefault(customerId,
                Array.Empty<SearchResourceTarget>()));

        public Task<IReadOnlyList<SearchResourceTarget>> GetDescendants(SearchResourceTarget collectionTarget,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchResourceTarget>>(Descendants.GetValueOrDefault(
                SearchDocumentId.Generate(collectionTarget), Array.Empty<SearchResourceTarget>()));

        public Task<IReadOnlyCollection<string>> GetAllDocumentIds(int customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>(AllDocumentIdsByCustomer.GetValueOrDefault(customerId,
                Array.Empty<string>()));
    }

    private sealed class FakeSearchDocumentBuilder : ISearchDocumentBuilder
    {
        public Dictionary<string, SearchDocument> Documents { get; } = [];

        public Task<SearchDocument?> BuildAsync(SearchResourceTarget target, CancellationToken cancellationToken = default)
        {
            Documents.TryGetValue(SearchDocumentId.Generate(target), out var document);
            return Task.FromResult<SearchDocument?>(document);
        }
    }
}
