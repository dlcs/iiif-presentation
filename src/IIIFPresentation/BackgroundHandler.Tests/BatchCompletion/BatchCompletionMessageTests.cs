using System.Text.Json;
using AWS.SQS;
using BackgroundHandler.BatchCompletion;
using FluentAssertions;

namespace BackgroundHandler.Tests.BatchCompletion;

public class BatchCompletionMessageTests
{
    private static QueueMessage ValidMessage(Dictionary<string, string>? attributes = null) =>
        new(
            """{"id":42,"customer":7,"count":10,"completed":9,"errors":1,"submitted":"2024-01-01T00:00:00Z","finished":"2024-01-02T00:00:00Z"}""",
            attributes ?? new Dictionary<string, string>(),
            "msg-1");

    [Fact]
    public void FromQueueMessage_DeserializesBodyProperties()
    {
        var message = ValidMessage();

        var result = BatchCompletionMessage.FromQueueMessage(message);

        result.Id.Should().Be(42);
        result.Customer.Should().Be(7);
        result.Count.Should().Be(10);
        result.Completed.Should().Be(9);
        result.Errors.Should().Be(1);
        result.Submitted.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        result.Finished.Should().Be(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void FromQueueMessage_TypeIsBatch_WhenNoTypeAttribute()
    {
        var message = ValidMessage();

        var result = BatchCompletionMessage.FromQueueMessage(message);

        result.Type.Should().Be(BatchCompletionType.Batch);
    }

    [Fact]
    public void FromQueueMessage_TypeIsBatch_WhenTypeAttributeIsEmpty()
    {
        var message = ValidMessage(new Dictionary<string, string> { ["Type"] = "" });

        var result = BatchCompletionMessage.FromQueueMessage(message);

        result.Type.Should().Be(BatchCompletionType.Batch);
    }

    [Theory]
    [InlineData("Batch")]
    [InlineData("batch")]
    [InlineData("BATCH")]
    public void FromQueueMessage_TypeIsBatch_WhenTypeAttributeIsBatch(string typeValue)
    {
        var message = ValidMessage(new Dictionary<string, string> { ["Type"] = typeValue });

        var result = BatchCompletionMessage.FromQueueMessage(message);

        result.Type.Should().Be(BatchCompletionType.Batch);
    }

    [Theory]
    [InlineData("AdjunctBatch")]
    [InlineData("adjunctbatch")]
    [InlineData("anything-else")]
    public void FromQueueMessage_TypeIsAdjunctBatch_WhenTypeAttributeIsNotBatch(string typeValue)
    {
        var message = ValidMessage(new Dictionary<string, string> { ["Type"] = typeValue });

        var result = BatchCompletionMessage.FromQueueMessage(message);

        result.Type.Should().Be(BatchCompletionType.AdjunctBatch);
    }

    [Fact]
    public void FromQueueMessage_DeserializesBodyProperties_WhenBodyContainsSuperseded()
    {
        // "Batch" completion messages contain superseded property, this confirms we can handle this 
        var message = new QueueMessage(
            """{"id":42,"customer":7,"count":10,"completed":9,"errors":1,"superseded":true,"submitted":"2024-01-01T00:00:00Z","finished":"2024-01-02T00:00:00Z"}""",
            new Dictionary<string, string> { ["Type"] = "Batch" },
            "msg-1");

        var result = BatchCompletionMessage.FromQueueMessage(message);

        result.Id.Should().Be(42);
        result.Customer.Should().Be(7);
        result.Type.Should().Be(BatchCompletionType.Batch);
    }

    [Fact]
    public void FromQueueMessage_Throws_WhenBodyIsInvalidJson()
    {
        var message = new QueueMessage("not-json", new Dictionary<string, string>(), "msg-2");

        var act = () => BatchCompletionMessage.FromQueueMessage(message);

        act.Should().Throw<JsonException>();
    }
}
