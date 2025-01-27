namespace DLCS;

public class DlcsSettings
{
    public const string SettingsName = "DLCS";
    
    /// <summary>
    /// URL root of DLCS API 
    /// </summary>
    public required Uri ApiUri { get; set; }
    
    /// <summary>
    /// URL root of DLCS Orchestrator 
    /// </summary>
    public Uri? OrchestratorUri { get; set; }
        
    /// <summary>
    /// Default timeout (in ms) use for HttpClient.Timeout in the API.
    /// </summary>
    public int ApiDefaultTimeoutMs { get; set; } = 30000;
    
    /// <summary>
    /// Default timeout (in ms) use for HttpClient.Timeout in orchestrator.
    /// </summary>
    public int OrchestratorDefaultTimeoutMs { get; set; } = 30000;
    
    /// <summary>
    /// The maximum size of an individual batch request
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// The name of the query used for retrieving images
    /// </summary>
    public string ManifestNamedQueryName { get; set; } = "batch-query";

    /// <summary>
    ///     The maximum number of images that can be requested
    /// </summary>
    public int MaxImageListSize { get; set; } = 500;
}
