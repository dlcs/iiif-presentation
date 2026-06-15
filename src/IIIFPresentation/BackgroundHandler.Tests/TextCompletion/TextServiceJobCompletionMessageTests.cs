using System.Text.Json;
using AWS.SQS;
using BackgroundHandler.TextCompletion;
using FluentAssertions;

namespace BackgroundHandler.Tests.TextCompletion;

public class TextServiceJobCompletionMessageTests
{
    private static QueueMessage ValidMessage() =>
        new(
            """{"jobId":"1/iiif/manifest-1","status":"Completed","finished":"2024-06-12T10:00:00Z","totalPages":5,"totalWordCount":1200,"errors":null}""",
            new Dictionary<string, string>(),
            "msg-1");

    [Fact]
    public void FromQueueMessage_DeserializesBodyProperties()
    {
        var result = TextServiceJobCompletionMessage.FromQueueMessage(ValidMessage());

        result.JobId.Should().Be("1/iiif/manifest-1");
        result.Status.Should().Be("Completed");
        result.Finished.Should().Be(new DateTimeOffset(2024, 6, 12, 10, 0, 0, TimeSpan.Zero));
        result.TotalPages.Should().Be(5);
        result.TotalWordCount.Should().Be(1200);
        result.Errors.Should().BeNull();
    }

    [Fact]
    public void FromQueueMessage_DeserializesErrors_WhenPresent()
    {
        var message = new QueueMessage(
            """{"jobId":"1/iiif/x","status":"Failed","finished":null,"totalPages":0,"totalWordCount":0,"errors":"OCR failed on page 3"}""",
            new Dictionary<string, string>(), "msg-err");

        var result = TextServiceJobCompletionMessage.FromQueueMessage(message);

        result.Errors.Should().Be("OCR failed on page 3");
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("completed")]
    [InlineData("COMPLETED")]
    public void IsCompleted_ReturnsTrue_CaseInsensitive(string status)
    {
        var message = new QueueMessage(
            $$"""{"jobId":"1/iiif/x","status":"{{status}}","finished":null,"totalPages":0,"totalWordCount":0,"errors":null}""",
            new Dictionary<string, string>(), "msg");

        TextServiceJobCompletionMessage.FromQueueMessage(message).IsCompleted.Should().BeTrue();
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("failed")]
    [InlineData("FAILED")]
    [InlineData("unknown")]
    public void IsCompleted_ReturnsFalse_WhenStatusIsNotCompleted(string status)
    {
        var message = new QueueMessage(
            $$"""{"jobId":"1/iiif/x","status":"{{status}}","finished":null,"totalPages":0,"totalWordCount":0,"errors":null}""",
            new Dictionary<string, string>(), "msg");

        TextServiceJobCompletionMessage.FromQueueMessage(message).IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void FromQueueMessage_Throws_WhenBodyIsInvalidJson()
    {
        var message = new QueueMessage("not-json", new Dictionary<string, string>(), "msg-bad");

        var act = () => TextServiceJobCompletionMessage.FromQueueMessage(message);

        act.Should().Throw<JsonException>();
    }
}