namespace AWS.Settings;

/// <summary>
/// Strongly typed S3 settings object 
/// </summary>
public class S3Settings
{
    /// <summary>
    /// Name of bucket for storing ingested web-friendly assets
    /// </summary>
    public string StorageBucket { get; set; }

    /// <summary>
    ///     Name of the bucket used as a staging location
    /// </summary>
    public string StagingStorageBucket => $"staging-{StorageBucket}";
    
    /// <summary>
    /// Service root for S3. Only used if running LocalStack
    /// </summary>
    public string ServiceUrl { get; set; } = "http://localhost:4566/";
}
