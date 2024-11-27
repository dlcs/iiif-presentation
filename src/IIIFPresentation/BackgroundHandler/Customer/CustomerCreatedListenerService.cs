using AWS.Settings;
using AWS.SQS;
using Core.Helpers;
using Microsoft.Extensions.Options;

namespace API.Features.CustomerCreation;

/// <summary>
/// Background service that monitors SQS queue for incoming messages that customer has been created
/// </summary>
public class CustomerCreatedListenerService(
    SqsListener sqsListener,
    IOptions<AWSSettings> awsSettings,
    ILogger<CustomerCreatedListenerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var customerCreatedQueueName =
            awsSettings.Value.SQS.CustomerCreatedQueueName?.ThrowIfNullOrWhiteSpace("queueName")!;

        logger.LogInformation("CustomerCreatedListenerService ExecuteAsync. Listening to {QueueName}",
            customerCreatedQueueName);
        await sqsListener.StartListenLoop<CustomerCreatedMessageHandler>(customerCreatedQueueName, stoppingToken);
    }
}