using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Services.Search;

public static class SearchServiceCollectionX
{
    public static IServiceCollection AddSearchServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TypesenseSettings>(configuration.GetSection(TypesenseSettings.SettingsName));

        var typesenseSettings = configuration.GetSection(TypesenseSettings.SettingsName).Get<TypesenseSettings>()
                               ?? new TypesenseSettings();

        if (!typesenseSettings.IsConfigured)
        {
            services
                .AddSingleton<ISearchSyncService, NoOpSearchSyncService>()
                .AddSingleton<ITypesenseClient, DisabledTypesenseClient>();
            return services;
        }

        services
            .AddHttpClient<ITypesenseClient, TypesenseClient>(client =>
            {
                client.BaseAddress = typesenseSettings.GetBaseUri();
                client.DefaultRequestHeaders.Add("X-TYPESENSE-API-KEY", typesenseSettings.ApiKey);
            })
            .Services
            .AddSingleton(sp => sp.GetRequiredService<IOptions<TypesenseSettings>>().Value)
            .AddScoped<ISearchDocumentBuilder, SearchDocumentBuilder>()
            .AddScoped<IChangedResourceEnumerator, ChangedResourceEnumerator>()
            .AddScoped<ISearchSyncStateStore, TypesenseSearchSyncStateStore>()
            .AddScoped<ISearchSyncService, SearchSyncService>();

        return services;
    }

    private class DisabledTypesenseClient : ITypesenseClient
    {
        public Task<TypesenseAlias?> GetAliasAsync(string aliasName, CancellationToken cancellationToken = default) =>
            Task.FromResult<TypesenseAlias?>(null);

        public Task UpsertAliasAsync(string aliasName, string collectionName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task CreateCollectionAsync(object schema, bool ignoreIfExists = false, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteCollectionAsync(string collectionName, bool ignoreIfMissing = false, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<TypesenseImportResult>> ImportDocumentsAsync(string collectionName, IEnumerable<object> documents,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TypesenseImportResult>>([]);

        public Task<JObject?> GetDocumentAsync(string collectionName, string documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<JObject?>(null);

        public Task<bool> DeleteDocumentAsync(string collectionName, string documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyCollection<string>> ExportDocumentIdsAsync(string collectionName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>([]);
    }
}
