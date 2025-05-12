using AWS.SQS;

namespace BackgroundHandler.Tests.Helpers;

public static class QueueHelper
{
    public static QueueMessage CreateQueueMessage(int batchId, int customerId, DateTime? finished = null)
    {
        var batchMessage = $@"
{{
    ""id"":{batchId},
    ""customerId"": {customerId},
    ""total"":1,
    ""success"":1,
    ""errors"":0,
    ""superseded"":false,
    ""started"":""2024-12-19T21:03:31.57Z"",
    ""finished"":""{finished ?? DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssK}""
}}";
        return new QueueMessage(batchMessage, new Dictionary<string, string>(), "foo");
    }
}
