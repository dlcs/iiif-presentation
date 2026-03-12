namespace Services.Search;

public static class SearchSchema
{
    public const int Version = 1;

    public static string GetStateCollectionName(TypesenseSettings settings) => $"{settings.CollectionAlias}__state";

    public static string GenerateCollectionName(TypesenseSettings settings) =>
        $"{settings.CollectionAlias}_v{Version}_{DateTime.UtcNow:yyyyMMddHHmmssfff}";

    public static object GetSearchCollectionSchema(string collectionName) => new
    {
        name = collectionName,
        enable_nested_fields = true,
        fields = new object[]
        {
            new { name = "id", type = "string" },
            new { name = "customer_id", type = "int32", facet = true },
            new { name = "resource_type", type = "string", facet = true },
            new { name = "flat_id", type = "string", facet = true },
            new { name = "public_id", type = "string", optional = true },
            new { name = "api_id", type = "string", optional = true },
            new { name = "slug", type = "string", optional = true },
            new { name = "full_path", type = "string", optional = true },
            new { name = "parent_flat_id", type = "string", optional = true, facet = true },
            new { name = "label", type = "string", optional = true },
            new { name = "summary_text", type = "string", optional = true },
            new { name = "metadata_text", type = "string", optional = true },
            new { name = "required_statement_text", type = "string", optional = true },
            new { name = "provider_text", type = "string", optional = true },
            new { name = "homepage_text", type = "string", optional = true },
            new { name = "see_also_text", type = "string", optional = true },
            new { name = "rendering_text", type = "string", optional = true },
            new { name = "rights", type = "string", optional = true, facet = true },
            new { name = "nav_date_ts", type = "int64", optional = true, sort = true },
            new { name = "thumbnail", type = "string", optional = true, index = false },
            new { name = "tags", type = "string[]", optional = true, facet = true },
            new { name = "is_public", type = "bool", facet = true },
            new { name = "is_processed", type = "bool", facet = true },
            new { name = "is_in_progress", type = "bool", facet = true },
            new { name = "modified_ts", type = "int64", sort = true },
            new { name = "last_processed_ts", type = "int64", optional = true, sort = true },
            new { name = "iiif_descriptive", type = "object", optional = true, index = false }
        }
    };

    public static object GetStateCollectionSchema(string collectionName) => new
    {
        name = collectionName,
        fields = new object[]
        {
            new { name = "id", type = "string" },
            new { name = "schema_version", type = "int32" },
            new { name = "active_collection", type = "string", optional = true },
            new { name = "last_synced_at", type = "int64", optional = true },
            new { name = "last_orphan_sweep_at", type = "int64", optional = true }
        }
    };
}
