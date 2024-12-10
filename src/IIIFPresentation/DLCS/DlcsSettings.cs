namespace DLCS;

public class DlcsSettings
{
    public const string SettingsName = "DLCS";
    
    /// <summary>
    /// URL root of DLCS API 
    /// </summary>
    public required Uri ApiUri { get; set; }
        
    /// <summary>
    /// Default timeout (in ms) use for HttpClient.Timeout.
    /// </summary>
    public int DefaultTimeoutMs { get; set; } = 30000;
    
    /// <summary>
    /// The maximum size of an individual batch request
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;
}
