using AWS.Settings;
using AWS.SQS;
using Core.Helpers;
using Microsoft.Extensions.Options;

namespace BackgroundHandler.Listener;

/// <summary>
/// Background service that monitors SQS queue for incoming messages that customer has been created
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