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
    IOptions<AWSSettings> awsSettings,
    ILogger<T> logger)
    : BackgroundService where T: IMessageHandler
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var customerCreatedQueueName =
            awsSettings.Value.SQS.CustomerCreatedQueueName?.ThrowIfNullOrWhiteSpace("queueName")!;

        logger.LogInformation("CustomerCreatedListenerService ExecuteAsync. Listening to {QueueName}",
            customerCreatedQueueName);
        await sqsListener.StartListenLoop<T>(customerCreatedQueueName, stoppingToken);
    }
}