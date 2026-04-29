using System.Text.Json;
using System.Text.Json.Serialization;
using AWS.SQS;
using Models.Database.General;

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

    public DeliverableType DeliverableType { get; private set; }

    public static BatchCompletionMessage FromQueueMessage(QueueMessage message)
    {
        var batchCompletionMessage =
            JsonSerializer.Deserialize<BatchCompletionMessage>(message.Body, JsonSerializerOptions)
            ?? throw new JsonException("Deserialized BatchCompletionMessage was null");

        if (message.Attributes.TryGetValue("Type", out var type) &&
            type.Equals("AdjunctBatch", StringComparison.OrdinalIgnoreCase))
        {
            batchCompletionMessage.DeliverableType = DeliverableType.Adjunct;
        }
        else
        {
            batchCompletionMessage.DeliverableType = DeliverableType.Asset;
        }

        return batchCompletionMessage;
    }
}
