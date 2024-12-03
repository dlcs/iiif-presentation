﻿using Amazon.SQS;
using Amazon.SQS.Model;
using AWS.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AWS.SQS;

/// <summary>
/// A collection of helper utilities for working with SQS queues
/// </summary>
public class SqsQueueUtilities
{
    private readonly IAmazonSQS client;
    private readonly ILogger<SqsQueueUtilities> logger;
    private readonly AWSSettings options;

    public SqsQueueUtilities(
        IAmazonSQS client,
        IOptions<AWSSettings> options,
        ILogger<SqsQueueUtilities> logger)
    {
        this.client = client;
        this.logger = logger;
        this.options = options.Value;
    }
        
    /// <summary>
    /// Get the URL of queue for specified name.
    /// </summary>
    /// <param name="queueName">Queue name to get URL for</param>
    /// <returns>SQS URL for queue</returns>
    public async Task<string> GetQueueUrl(string queueName, CancellationToken cancellationToken = default)
    {
        // Having this here isn't great; alternative is a different entrypoint with similar logic
        var usingLocalStack = options.UseLocalStack;
        var count = 0;

        do
        {
            try
            {
                var result = await client.GetQueueUrlAsync(queueName, cancellationToken);
                return result.QueueUrl;
            }
            catch (QueueDoesNotExistException qEx)
            {
                logger.LogError(qEx, "Attempt to get url for queue '{Queue}' but it doesn't exist", queueName);
                if (!usingLocalStack) throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "General error attempting to get url for queue '{Queue}'", queueName);
                if (!usingLocalStack) throw;
            }

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        } while (usingLocalStack && count++ < 10);

        throw new ApplicationException("Using localStack but unable to get queue Id after 10 attempts");
    }
}