namespace Services.Search;

public class TypesenseSettings
{
    public const string SettingsName = "Typesense";

    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public string CollectionPrefix { get; set; } = "iiif_presentation";

    public List<int> WhitelistCustomerIds { get; set; } = [];

    public List<int> BlacklistCustomerIds { get; set; } = [];

    public int BatchWindowMinutes { get; set; } = 5;

    public int ImportBatchSize { get; set; } = 100;

    public int BootstrapBatchSize { get; set; } = 500;

    public int OrphanSweepIntervalHours { get; set; } = 24;

    public bool HasConnectionSettings =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(ApiKey);

    public bool IsConfigured => HasConnectionSettings && !string.IsNullOrWhiteSpace(CollectionPrefix);

    public Uri? GetBaseUri() => Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ? uri : null;

    public bool IsCustomerIncluded(int customerId)
    {
        if (WhitelistCustomerIds.Count > 0)
        {
            return WhitelistCustomerIds.Contains(customerId);
        }

        return !BlacklistCustomerIds.Contains(customerId);
    }
}
