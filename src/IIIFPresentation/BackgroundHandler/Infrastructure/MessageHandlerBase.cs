using AWS.SQS;
using BackgroundHandler.Helpers;

namespace BackgroundHandler.Infrastructure;

/// <summary>
/// Base class for <see cref="IMessageHandler"/> implementations. Owns the common message-handling skeleton:
/// log-context setup, deserialisation (with error logging) and the top-level try/catch. Implementers provide
/// deserialisation of the message body and handling of the deserialized message.
/// </summary>
/// <typeparam name="TMessage">Type the queue message body deserializes to.</typeparam>
public abstract class MessageHandlerBase<TMessage>(ILogger logger) : IMessageHandler
{
    protected ILogger Logger { get; } = logger;

    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        using (LogContextHelpers.SetServiceName(GetType().Name, message.MessageId))
        {
            try
            {
                var deserialized = Deserialize(message);
                return await HandleMessage(deserialized, message, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling {ServiceName} message {MessageId}",
                    GetType().Name, message.MessageId);
            }
        }

        return false;
    }

    /// <summary>
    /// Handle the deserialized message. Return true to delete the message from the queue, false to retry.
    /// </summary>
    protected abstract Task<bool> HandleMessage(TMessage message, QueueMessage rawMessage,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deserialize the queue message body into <typeparamref name="TMessage"/>.
    /// </summary>
    protected abstract TMessage DeserializeMessage(QueueMessage message);

    private TMessage Deserialize(QueueMessage message)
    {
        try
        {
            return DeserializeMessage(message);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not deserialize {ServiceName} message", GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Decide whether a message referencing a resource not (yet) tracked in presentation should be discarded.
    /// Allows a few retries to cover timing/races, then discards.
    /// </summary>
    protected bool DiscardUntrackedResource(int approximateReceiveCount, string resourceDescription,
        int retryThreshold = 2)
    {
        var discard = approximateReceiveCount >= retryThreshold;
        Logger.LogTrace("{Resource} not found in presentation. ApproximateReceiveCount:{Count}. {Action}",
            resourceDescription, approximateReceiveCount, discard ? "Discarding" : "Will retry");
        return discard;
    }
}
