using System.Diagnostics;
using AWS.SQS;
using BackgroundHandler.Helpers;
using Microsoft.EntityFrameworkCore;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Services.Manifests.AWS;

namespace BackgroundHandler.BatchCompletion;

public class BatchCompletionMessageHandler(
    PresentationContext dbContext,
    ICustomerIdProvider customerIdProvider,
    IManifestStorageManager manifestS3Manager,
    ILogger<BatchCompletionMessageHandler> logger)
    : IMessageHandler
{
    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        using (LogContextHelpers.SetServiceName(nameof(BatchCompletionMessageHandler), message.MessageId))
        {
            try
            {
                var batchCompletionMessage = DeserializeMessage(message, logger);
                
                customerIdProvider.SetCustomerId(batchCompletionMessage.Customer);
                
                return await TryUpdateManifest(batchCompletionMessage, message.ApproximateReceiveCount, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling batch-completion message {MessageId}", message.MessageId);
            }
        }
        
        return false;
    }

    private async Task<bool> TryUpdateManifest(BatchCompletionMessage batchCompletionMessage, int approximateReceiveCount, CancellationToken cancellationToken)
    {
        // Load batch the incoming message is referring to
        var batch = await dbContext.Batches.Include(b => b.Manifest)
            .ThenInclude(m => m.CanvasPaintings)
            .SingleOrDefaultAsync(
                b => b.Id == batchCompletionMessage.Id && b.DeliverableType == batchCompletionMessage.DeliverableType,
                cancellationToken);

        // batch isn't tracked by presentation - allow a few retries in case of a timing issue
        if (batch == null)
        {
            var discard = approximateReceiveCount >= 2;
            logger.LogTrace(
                "Batch {BatchId} not found in presentation. ApproximateReceiveCount:{Count}. {Action}",
                batchCompletionMessage.Id, approximateReceiveCount, discard ? "Discarding" : "Will retry");
            return discard;
        }

        var sw = Stopwatch.StartNew();

        // Other batches haven't completed, so can't populate Manifest until all are complete
        if (await dbContext.Batches.AnyAsync(b => b.ManifestId == batch.ManifestId &&
                                                  b.Status != BatchStatus.Completed &&
                                                  b.Id != batch.Id, cancellationToken))
        {
            TryCompleteBatch(batch, batchCompletionMessage.Finished);
        }
        else
        {
            logger.LogInformation(
                "Attempting to complete assets in batch:{BatchId}, customer:{CustomerId}, manifest:{ManifestId}",
                batch.Id, batch.CustomerId, batch.ManifestId);

            try
            {
                if (TryCompleteBatch(batch, batchCompletionMessage.Finished))
                {
                    await manifestS3Manager.UpsertManifestFromStagingInStorage(batch.Manifest!, cancellationToken);
                }
                else
                {
                    logger.LogInformation(
                        "Batch:{BatchId}, customer:{CustomerId}, manifest:{ManifestId} already completed",
                        batch.Id, batch.CustomerId, batch.ManifestId);
                    return true;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error updating completing batch {BatchId} for manifest {ManifestId}", batch.Id,
                    batch.ManifestId);
                return false;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Updating batch:{BatchId}, customer:{CustomerId}, manifest:{ManifestId}. Completed in {Elapsed}ms",
            batch.Id, batch.CustomerId, batch.ManifestId, sw.ElapsedMilliseconds);
        return true;
    }
    
    private static BatchCompletionMessage DeserializeMessage(QueueMessage message, ILogger logger)
    {
        try
        {
            return BatchCompletionMessage.FromQueueMessage(message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not deserialize message");
            throw;
        }
    }
    
    /// <summary>
    /// Attempt to complete the batch if it hasn't already been marked as complete. This can happen in instances where
    /// the SQS is either re-delivered (unlikely) or the batch auto-completed in the API, and the API already marked
    /// this batch as complete.
    /// </summary>
    private static bool TryCompleteBatch(Batch batch, DateTime finished)
    { 
        if (batch.Status == BatchStatus.Completed) return false;
        
        batch.Processed = DateTime.UtcNow;
        batch.Finished = finished;
        batch.Status = BatchStatus.Completed;
        return true;
    }
}
