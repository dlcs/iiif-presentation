namespace AWS.Settings;

public class AWSSettings
{
    /// <summary>
    /// If true, service will use LocalStack and custom ServiceUrl
    /// </summary>
    public bool UseLocalStack { get; set; } = false;
    
    /// <summary>
    /// S3 Settings
    /// </summary>
    public S3Settings S3 { get; set; } = new();
    
    /// <summary>
    /// SQS Settings
    /// </summary>
    public SQSSettings SQS { get; set; } = new();
}