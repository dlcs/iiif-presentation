using System.Text.Json;
using System.Text.Json.Serialization;
using AWS.SQS;

namespace BackgroundHandler.BatchCompletion;

/// <summary>
/// Represents a batch completion message from IIIF CloudServices.
/// </summary>
[method: JsonConstructor]
public class BatchCompletionMessage(
    int id,
    int customer,
    int count,
    int completed,
    int errors,
    DateTime submitted,
    DateTime finished)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public int Id { get; } = id;

    public int Customer { get; } = customer;

    public int Count { get; } = count;

    public int Completed { get; } = completed;

    public int Errors { get; } = errors;

    public DateTime Submitted { get; } = submitted;

    public DateTime Finished { get; } = finished;

    public BatchCompletionType Type { get; private set; }

    public static BatchCompletionMessage FromQueueMessage(QueueMessage message)
    {
        var batchCompletionMessage =
            JsonSerializer.Deserialize<BatchCompletionMessage>(message.Body, JsonSerializerOptions)
            ?? throw new JsonException("Deserialized BatchCompletionMessage was null");

        if (!message.Attributes.TryGetValue("Type", out var type) || string.IsNullOrEmpty(type))
        {
            batchCompletionMessage.Type = BatchCompletionType.Batch;
        }
        else
        {
            batchCompletionMessage.Type = type.Equals("Batch", StringComparison.OrdinalIgnoreCase)
                ? BatchCompletionType.Batch
                : BatchCompletionType.AdjunctBatch;
        }

        return batchCompletionMessage;
    }
}

// TODO - check if this is already in API once merged
public enum BatchCompletionType
{
    Unknown,
    Batch,
    AdjunctBatch
}
