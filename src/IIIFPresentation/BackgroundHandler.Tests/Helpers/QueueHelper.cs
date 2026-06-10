using Amazon.SQS.Model;
using AWS.SQS;
using Models.Database.General;

namespace BackgroundHandler.Tests.Helpers;

public static class QueueHelper
{
    public static QueueMessage CreateQueueMessage(int batchId, int customerId, DateTime? finished = null,
        DeliverableType deliverableType = DeliverableType.Asset, int approximateReceiveCount = 0)
    {
        var batchMessage = $@"
{{
    ""id"":{batchId},
    ""customer"": {customerId},
    ""count"":1,
    ""completed"":1,
    ""errors"":0,
    ""superseded"":false,
    ""submitted"":""2024-12-19T21:03:31.57Z"",
    ""finished"":""{finished ?? DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssK}""
}}";
        var messageAttributes = new Dictionary<string, MessageAttributeValue>
        {
            ["Type"] = new MessageAttributeValue { StringValue = deliverableType == DeliverableType.Asset ? "Batch" : "AdjunctBatch" }
        };
        var systemAttributes = new Dictionary<string, string>
        {
            ["ApproximateReceiveCount"] = approximateReceiveCount.ToString()
        };
        return new QueueMessage(batchMessage, messageAttributes, systemAttributes, "foo");
    }
}
