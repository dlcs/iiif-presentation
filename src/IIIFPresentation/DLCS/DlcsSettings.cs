namespace DLCS;

public class DlcsSettings
{
    public const string SettingsName = "DLCS";
    
    /// <summary>
    /// URL root of DLCS API 
    /// </summary>
    public required Uri ApiUri { get; set; }
    
    /// <summary>
    /// URL root of DLCS API 
    /// </summary>
    public Uri? OrchestratorUri { get; set; }
        
    /// <summary>
    /// Default timeout (in ms) use for HttpClient.Timeout.
    /// </summary>
    public int DefaultTimeoutMs { get; set; } = 30000;
    
    /// <summary>
    /// The maximum size of an individual batch request
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;
    
    /// <summary>
    /// Used to authenticate requests that do not go via the HttpContextAccessor
    /// </summary>
    public string? ApiLocalAuth { get; set; }
}
