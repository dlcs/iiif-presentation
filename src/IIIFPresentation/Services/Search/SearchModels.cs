using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Services.Search;

public enum SearchResourceType
{
    StorageCollection,
    IiifCollection,
    Manifest
}

public record SearchResourceTarget(int CustomerId, string FlatId, SearchResourceType ResourceType);

public static class SearchDocumentId
{
    public static string Generate(int customerId, SearchResourceType resourceType, string flatId)
        => $"{customerId}:{resourceType.ToTypesenseValue()}:{flatId}";

    public static string Generate(SearchResourceTarget target) => Generate(target.CustomerId, target.ResourceType, target.FlatId);

    public static string ToTypesenseValue(this SearchResourceType resourceType) => resourceType switch
    {
        SearchResourceType.StorageCollection => "storage_collection",
        SearchResourceType.IiifCollection => "iiif_collection",
        SearchResourceType.Manifest => "manifest",
        _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null)
    };
}

public class SearchDocument
{
    [JsonProperty("id")] public required string Id { get; set; }
    [JsonProperty("customer_id")] public int CustomerId { get; set; }
    [JsonProperty("resource_type")] public required string ResourceType { get; set; }
    [JsonProperty("flat_id")] public required string FlatId { get; set; }
    [JsonProperty("public_id")] public required string PublicId { get; set; }
    [JsonProperty("api_id")] public required string ApiId { get; set; }
    [JsonProperty("slug")] public required string Slug { get; set; }
    [JsonProperty("full_path")] public string FullPath { get; set; } = string.Empty;
    [JsonProperty("parent_flat_id", NullValueHandling = NullValueHandling.Ignore)] public string? ParentFlatId { get; set; }
    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)] public string? Label { get; set; }
    [JsonProperty("summary_text", NullValueHandling = NullValueHandling.Ignore)] public string? SummaryText { get; set; }
    [JsonProperty("metadata_text", NullValueHandling = NullValueHandling.Ignore)] public string? MetadataText { get; set; }
    [JsonProperty("required_statement_text", NullValueHandling = NullValueHandling.Ignore)] public string? RequiredStatementText { get; set; }
    [JsonProperty("provider_text", NullValueHandling = NullValueHandling.Ignore)] public string? ProviderText { get; set; }
    [JsonProperty("homepage_text", NullValueHandling = NullValueHandling.Ignore)] public string? HomepageText { get; set; }
    [JsonProperty("see_also_text", NullValueHandling = NullValueHandling.Ignore)] public string? SeeAlsoText { get; set; }
    [JsonProperty("rendering_text", NullValueHandling = NullValueHandling.Ignore)] public string? RenderingText { get; set; }
    [JsonProperty("rights", NullValueHandling = NullValueHandling.Ignore)] public string? Rights { get; set; }
    [JsonProperty("nav_date_ts", NullValueHandling = NullValueHandling.Ignore)] public long? NavDateTimestamp { get; set; }
    [JsonProperty("thumbnail", NullValueHandling = NullValueHandling.Ignore)] public string? Thumbnail { get; set; }
    [JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)] public string[]? Tags { get; set; }
    [JsonProperty("is_public")] public bool IsPublic { get; set; }
    [JsonProperty("is_processed")] public bool IsProcessed { get; set; }
    [JsonProperty("is_in_progress")] public bool IsInProgress { get; set; }
    [JsonProperty("modified_ts")] public long ModifiedTimestamp { get; set; }
    [JsonProperty("last_processed_ts", NullValueHandling = NullValueHandling.Ignore)] public long? LastProcessedTimestamp { get; set; }
    [JsonProperty("iiif_descriptive", NullValueHandling = NullValueHandling.Ignore)] public JObject? IiifDescriptive { get; set; }
}

public class SearchSyncState
{
    [JsonProperty("id")] public required string Id { get; set; }
    [JsonProperty("schema_version")] public int SchemaVersion { get; set; }
    [JsonProperty("active_collection", NullValueHandling = NullValueHandling.Ignore)] public string? ActiveCollection { get; set; }
    [JsonProperty("last_synced_at", NullValueHandling = NullValueHandling.Ignore)] public long? LastSyncedAt { get; set; }
    [JsonProperty("last_orphan_sweep_at", NullValueHandling = NullValueHandling.Ignore)] public long? LastOrphanSweepAt { get; set; }

    [JsonIgnore]
    public DateTime? LastSyncedAtUtc
    {
        get => LastSyncedAt.HasValue ? DateTimeOffset.FromUnixTimeSeconds(LastSyncedAt.Value).UtcDateTime : null;
        set => LastSyncedAt = value.HasValue ? new DateTimeOffset(value.Value).ToUnixTimeSeconds() : null;
    }

    [JsonIgnore]
    public DateTime? LastOrphanSweepAtUtc
    {
        get => LastOrphanSweepAt.HasValue ? DateTimeOffset.FromUnixTimeSeconds(LastOrphanSweepAt.Value).UtcDateTime : null;
        set => LastOrphanSweepAt = value.HasValue ? new DateTimeOffset(value.Value).ToUnixTimeSeconds() : null;
    }
}

public class TypesenseAlias
{
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("collection_name")] public string? CollectionName { get; set; }
}

public class TypesenseImportResult
{
    [JsonProperty("success")] public bool Success { get; set; }
    [JsonProperty("error")] public string? Error { get; set; }
}
