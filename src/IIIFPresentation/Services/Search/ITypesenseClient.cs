using Newtonsoft.Json.Linq;

namespace Services.Search;

public interface ITypesenseClient
{
    Task<TypesenseAlias?> GetAliasAsync(string aliasName, CancellationToken cancellationToken = default);
    Task UpsertAliasAsync(string aliasName, string collectionName, CancellationToken cancellationToken = default);
    Task CreateCollectionAsync(object schema, bool ignoreIfExists = false, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(string collectionName, bool ignoreIfMissing = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TypesenseImportResult>> ImportDocumentsAsync(string collectionName, IEnumerable<object> documents,
        CancellationToken cancellationToken = default);
    Task<JObject?> GetDocumentAsync(string collectionName, string documentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(string collectionName, string documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> ExportDocumentIdsAsync(string collectionName, CancellationToken cancellationToken = default);
}
