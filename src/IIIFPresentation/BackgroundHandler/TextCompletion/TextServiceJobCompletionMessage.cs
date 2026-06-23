using System.Text.Json;
using System.Text.Json.Serialization;
using AWS.SQS;
using Models.Database.General;

namespace BackgroundHandler.TextCompletion;

/// <summary>
/// Represents a job-completion notification from text-services, matching JobCompletionNotification.
/// </summary>
public class TextServiceJobCompletionMessage(
    string jobId,
    PipelineJobStatus status,
    DateTimeOffset? finished,
    int totalPages,
    int totalWordCount,
    string? errors)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public string JobId { get; } = jobId;

    public PipelineJobStatus Status { get; } = status;

    public DateTimeOffset? Finished { get; } = finished;

    public int TotalPages { get; } = totalPages;

    public int TotalWordCount { get; } = totalWordCount;

    public string? Errors { get; } = errors;

    public bool IsCompleted => Status == PipelineJobStatus.Completed;

    public static TextServiceJobCompletionMessage FromQueueMessage(QueueMessage message) =>
        JsonSerializer.Deserialize<TextServiceJobCompletionMessage>(message.Body, JsonSerializerOptions)
        ?? throw new JsonException("Deserialized TextServiceJobCompletionMessage was null");
}
