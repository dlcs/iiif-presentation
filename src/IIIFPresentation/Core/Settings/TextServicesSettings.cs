namespace Core.Settings;

public class TextServicesSettings
{
    public const string SettingsName = "TextServices";

    /// <summary>
    /// Base URI for the text-services Builder API (POST/PUT /textbuilder)
    /// </summary>
    public Uri? BuilderApiUri { get; set; }

    /// <summary>
    /// Timeout (in seconds) for the text-services Builder API.
    /// </summary>
    public int BuilderApiTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Base URI for the text-services Search API (GET /text-augmented/v3/)
    /// </summary>
    public Uri? SearchApiUri { get; set; }
    
    /// <summary>
    /// Timeout (in seconds) for the text-services Search API.
    /// </summary>
    public int SearchApiTimeoutSeconds { get; set; } = 30;
}
