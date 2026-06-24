namespace Core.Settings;

public class TextServicesSettings
{
    public const string SettingsName = "TextServices";

    /// <summary>
    /// Base URI for the text-services Builder API (POST/PUT /textbuilder)
    /// </summary>
    public Uri? BuilderApiUri { get; set; }

    /// <summary>
    /// Base URI for the text-services Search API (GET /text-augmented/v3/)
    /// </summary>
    public Uri? SearchApiUri { get; set; }

    /// <summary>
    /// Used as the X-Forwarded-Host header when calling /text-augmented/v3.
    /// Falls back to default host if not set.
    /// </summary>
    public string? CustomerOrchestratorUri { get; set; }

    /// <summary>
    /// Used as the X-Forwarded-Path header when calling /text-augmented/v3
    /// </summary>
    public string? PathRules { get; set; }
}
