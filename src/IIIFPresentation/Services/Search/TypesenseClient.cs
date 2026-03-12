using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Services.Search;

public class TypesenseClient(HttpClient httpClient) : ITypesenseClient
{
    public async Task<TypesenseAlias?> GetAliasAsync(string aliasName, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/aliases/{aliasName}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        return await ReadJsonAsync<TypesenseAlias>(response, cancellationToken);
    }

    public async Task UpsertAliasAsync(string aliasName, string collectionName, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsync($"/aliases/{aliasName}",
            ToJsonContent(new { collection_name = collectionName }), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteAliasAsync(string aliasName, bool ignoreIfMissing = false, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/aliases/{aliasName}", cancellationToken);
        if (ignoreIfMissing && response.StatusCode == HttpStatusCode.NotFound) return;

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task CreateCollectionAsync(object schema, bool ignoreIfExists = false, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync("/collections", ToJsonContent(schema), cancellationToken);
        if (ignoreIfExists && response.StatusCode == HttpStatusCode.Conflict) return;

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteCollectionAsync(string collectionName, bool ignoreIfMissing = false, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/collections/{collectionName}", cancellationToken);
        if (ignoreIfMissing && response.StatusCode == HttpStatusCode.NotFound) return;

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<TypesenseImportResult>> ImportDocumentsAsync(string collectionName, IEnumerable<object> documents,
        CancellationToken cancellationToken = default)
    {
        var payload = string.Join('\n', documents.Select(JsonConvert.SerializeObject));
        if (string.IsNullOrWhiteSpace(payload))
        {
            return [];
        }

        using var content = new StringContent(payload, Encoding.UTF8, "text/plain");
        var response = await httpClient.PostAsync($"/collections/{collectionName}/documents/import?action=upsert", content,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return responseBody
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonConvert.DeserializeObject<TypesenseImportResult>(line) ?? new TypesenseImportResult
            {
                Success = false,
                Error = "Unable to parse Typesense import response"
            })
            .ToList();
    }

    public async Task<JObject?> GetDocumentAsync(string collectionName, string documentId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/collections/{collectionName}/documents/{Uri.EscapeDataString(documentId)}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        return await ReadJsonAsync<JObject>(response, cancellationToken);
    }

    public async Task<bool> DeleteDocumentAsync(string collectionName, string documentId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync(
            $"/collections/{collectionName}/documents/{Uri.EscapeDataString(documentId)}?ignore_not_found=true",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<JObject>> ExportDocumentsAsync(string collectionName, string? includeFields = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"/collections/{collectionName}/documents/export";
        if (!string.IsNullOrWhiteSpace(includeFields))
        {
            path += $"?include_fields={Uri.EscapeDataString(includeFields)}";
        }

        var response = await httpClient.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return responseBody
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(JObject.Parse)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<string>> ExportDocumentIdsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        var documents = await ExportDocumentsAsync(collectionName, "id", cancellationToken);
        return documents
            .Select(document => document["id"]?.Value<string>())
            .OfType<string>()
            .ToArray();
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonConvert.DeserializeObject<T>(body)
               ?? throw new InvalidOperationException($"Unable to deserialize Typesense response into {typeof(T).Name}");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? errorMessage = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                errorMessage = JObject.Parse(body)["message"]?.Value<string>();
            }
            catch (Exception)
            {
                errorMessage = body;
            }
        }

        errorMessage ??= response.ReasonPhrase ?? "Unknown Typesense error";
        throw new TypesenseException(errorMessage, response.StatusCode);
    }

    private static StringContent ToJsonContent(object value) =>
        new(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json");
}

public class TypesenseException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
