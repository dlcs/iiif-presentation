namespace AWS.SQS;

/// <summary>
/// Generic representation of message pulled from queue.
/// </summary>
public class QueueMessage(string body, Dictionary<string, string> attributes, string messageId)
{
    public string Body { get; } = body;

    public Dictionary<string, string> Attributes { get; } = attributes;

    public string MessageId { get; } = messageId;
}
