namespace AWS.SQS;

/// <summary>
/// Generic representation of message pulled from queue.
/// </summary>
public class QueueMessage
{
    public string Body { get; }

    public Dictionary<string, string> Attributes { get; }
        
    public string MessageId { get; }

    public QueueMessage(string body, Dictionary<string, string> attributes, string messageId)
    {
        Body = body;
        Attributes = attributes;
        MessageId = messageId;
    }
}