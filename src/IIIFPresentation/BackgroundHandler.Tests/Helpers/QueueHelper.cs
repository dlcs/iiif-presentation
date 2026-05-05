using AWS.SQS;
using Models.Database.General;

namespace BackgroundHandler.Tests.Helpers;

public static class QueueHelper
{
    public static QueueMessage CreateQueueMessage(int batchId, int customerId, DateTime? finished = null,
        DeliverableType deliverableType = DeliverableType.Asset)
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
        return new QueueMessage(batchMessage, new Dictionary<string, string>
        {
            ["Type"] = deliverableType == DeliverableType.Asset ? "Batch" : "AdjunctBatch"
        }, "foo");
    }
}
