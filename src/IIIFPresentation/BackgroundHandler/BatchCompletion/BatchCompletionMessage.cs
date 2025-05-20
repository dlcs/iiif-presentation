using System.Text.Json.Serialization;

namespace BackgroundHandler.BatchCompletion;

public class BatchCompletionMessage
{
    [JsonRequired]
    public int Id { get; set; }
    
    [JsonRequired]
    public int Customer { get; set; }
    
    [JsonRequired]
    public required int Count { get; set; }
    
    [JsonRequired]
    public int Completed { get; set; }
    
    [JsonRequired]
    public int Errors { get; set; }
    
    [JsonRequired]
    public bool Superseded { get; set; }
    
    [JsonRequired]
    public DateTime Submitted { get; set; }
    
    [JsonRequired]
    public DateTime Finished { get; set; }
}

/// <summary>
/// Uses a non-standard version of the batch completion message that was used initially - this can be removed once
/// protagonist has been updated to use the version specified above everywhere and just the <see cref="BatchCompletionMessage"/>
/// used
/// </summary>
public class OldBatchCompletionMessage
{
    public int Id { get; set; }
    
    public int CustomerId { get; set; }
    
    public required int Total { get; set; }
    
    public int Success { get; set; }
    
    public int Errors { get; set; }
    
    public bool Superseded { get; set; }
    
    public DateTime Started { get; set; }
    
    public DateTime Finished { get; set; }
}

public static class BatchCompletionMessageX
{
    public static BatchCompletionMessage ConvertBatchCompletionMessage(
        this OldBatchCompletionMessage batchCompletionMessage) => new()
    {
        Id = batchCompletionMessage.Id,
        Customer = batchCompletionMessage.CustomerId,
        Count = batchCompletionMessage.Total,
        Completed = batchCompletionMessage.Success,
        Errors = batchCompletionMessage.Errors,
        Superseded = batchCompletionMessage.Superseded,
        Submitted = batchCompletionMessage.Started,
        Finished = batchCompletionMessage.Finished
    };
}
