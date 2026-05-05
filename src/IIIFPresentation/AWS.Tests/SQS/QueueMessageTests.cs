using Amazon.SQS.Model;
using AWS.SQS;
using FluentAssertions;

namespace AWS.Tests.SQS;

public class QueueMessageTests
{
    [Fact]
    public void Constructor_WithStringAttributes_SetsPropertiesCorrectly()
    {
        // Arrange
        const string body = "Test message body";
        const string messageId = "test-message-id";
        var attributes = new Dictionary<string, string>
        {
            { "Attr1", "Value1" },
            { "Attr2", "Value2" }
        };

        // Act
        var queueMessage = new QueueMessage(body, attributes, messageId);

        // Assert
        queueMessage.Body.Should().Be(body);
        queueMessage.MessageId.Should().Be(messageId);
        queueMessage.Attributes.Should().Equal(attributes);
    }

    [Fact]
    public void Constructor_WithMessageAttributeValues_ExtractsStringValuesAndSetsPropertiesCorrectly()
    {
        // Arrange
        const string body = "Test message body";
        const string messageId = "test-message-id";
        var attributeValues = new Dictionary<string, MessageAttributeValue>
        {
            { "Attr1", new MessageAttributeValue { StringValue = "Value1" } },
            { "Attr2", new MessageAttributeValue { StringValue = "Value2" } },
            { "Attr3", new MessageAttributeValue { StringValue = "Value3" } }
        };

        // Act
        var queueMessage = new QueueMessage(body, attributeValues, messageId);

        // Assert
        queueMessage.Body.Should().Be(body);
        queueMessage.MessageId.Should().Be(messageId);
        queueMessage.Attributes.Should().Equal(new Dictionary<string, string>
        {
            { "Attr1", "Value1" },
            { "Attr2", "Value2" },
            { "Attr3", "Value3" }
        });
    }

    [Fact]
    public void Constructor_WithMessageAttributeValues_HandlesEmptyAttributes()
    {
        // Arrange
        const string body = "Test message body";
        const string messageId = "test-message-id";
        var attributeValues = new Dictionary<string, MessageAttributeValue>();

        // Act
        var queueMessage = new QueueMessage(body, attributeValues, messageId);

        // Assert
        queueMessage.Body.Should().Be(body);
        queueMessage.MessageId.Should().Be(messageId);
        queueMessage.Attributes.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithMessageAttributeValues_PreservesAttributeOrder()
    {
        // Arrange
        const string body = "Test message body";
        const string messageId = "test-message-id";
        var attributeValues = new Dictionary<string, MessageAttributeValue>
        {
            { "Z_Attr", new MessageAttributeValue { StringValue = "ValueZ" } },
            { "A_Attr", new MessageAttributeValue { StringValue = "ValueA" } },
            { "M_Attr", new MessageAttributeValue { StringValue = "ValueM" } }
        };

        // Act
        var queueMessage = new QueueMessage(body, attributeValues, messageId);

        // Assert
        queueMessage.Attributes.Keys.Should().ContainInOrder("Z_Attr", "A_Attr", "M_Attr");
    }

    [Fact]
    public void Constructor_WithMessageAttributeValues_HandlesNullStringValue()
    {
        // Arrange
        const string body = "Test message body";
        const string messageId = "test-message-id";
        var attributeValues = new Dictionary<string, MessageAttributeValue>
        {
            { "Attr1", new MessageAttributeValue { StringValue = "Value1" } },
            { "AttrWithNullValue", new MessageAttributeValue { StringValue = null } }
        };

        // Act
        var queueMessage = new QueueMessage(body, attributeValues, messageId);

        // Assert
        queueMessage.Attributes.Should().Contain("Attr1", "Value1");
        queueMessage.Attributes["AttrWithNullValue"].Should().BeNull();
    }
}
