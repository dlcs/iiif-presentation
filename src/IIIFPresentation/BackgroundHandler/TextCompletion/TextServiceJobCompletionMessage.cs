using System.Text.Json;
using System.Text.Json.Serialization;
using AWS.SQS;

namespace BackgroundHandler.TextCompletion;

/// <summary>
/// Represents a job-completion notification from text-services, matching JobCompletionNotification.
/// </summary>
[method: JsonConstructor]
public class TextServiceJobCompletionMessage(
    string jobId,
    string status,
    DateTimeOffset? finished,
    int totalPages,
    int totalWordCount,
    string? errors)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public string JobId { get; } = jobId;

    /// <summary>"Completed" or "Failed"</summary>
    public string Status { get; } = status;

    public DateTimeOffset? Finished { get; } = finished;

    public int TotalPages { get; } = totalPages;

    public int TotalWordCount { get; } = totalWordCount;

    public string? Errors { get; } = errors;

    public bool IsCompleted => string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase);

    public static TextServiceJobCompletionMessage FromQueueMessage(QueueMessage message) =>
        JsonSerializer.Deserialize<TextServiceJobCompletionMessage>(message.Body, JsonSerializerOptions)
        ?? throw new JsonException("Deserialized TextServiceJobCompletionMessage was null");
}