using Amazon.SQS.Model;

namespace AWS.SQS;

/// <summary>
/// Generic representation of message pulled from queue.
/// </summary>
public class QueueMessage
{
    public QueueMessage(string body, Dictionary<string, string> attributes, string messageId)
    {
        Body = body;
        Attributes = attributes;
        MessageId = messageId;
    }
    
    /// <remarks>
    /// This only maps StringValues from attributes, it won't handle other types (BinaryValue or MemoryStream)
    /// </remarks>
    public QueueMessage(string body, Dictionary<string, MessageAttributeValue> attributes, string messageId)
    {
        Body = body;
        Attributes = attributes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.StringValue);
        MessageId = messageId;
    }

    public string Body { get; }

    public Dictionary<string, string> Attributes { get; }

    public string MessageId { get; }
}
