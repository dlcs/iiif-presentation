namespace Services.Search;

public class TypesenseSettings
{
    public const string SettingsName = "Typesense";

    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public string CollectionAlias { get; set; } = "iiif_presentation";

    public int BatchWindowMinutes { get; set; } = 5;

    public int ImportBatchSize { get; set; } = 100;

    public int BootstrapBatchSize { get; set; } = 500;

    public int OrphanSweepIntervalHours { get; set; } = 24;

    public bool IsConfigured =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(ApiKey);

    public Uri? GetBaseUri() => Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ? uri : null;
}
