using AWS.SQS;

namespace BackgroundHandler.Listener;

/// <summary>
/// Background service that monitors SQS queue and handles messages with specified <see cref="IMessageHandler"/>
/// </summary>
public class CreateBackgroundListenerService<T>(
    SqsListener sqsListener,
    string queueName,
    ILogger<T> logger)
    : BackgroundService where T: IMessageHandler
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("{Type} ExecuteAsync. Listening to {QueueName}", typeof(T).Name,
            queueName);
        await sqsListener.StartListenLoop<T>(queueName, stoppingToken);
    }
}
