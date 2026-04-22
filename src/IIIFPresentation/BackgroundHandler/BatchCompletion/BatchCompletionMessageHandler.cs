using System.Diagnostics;
using AWS.SQS;
using BackgroundHandler.Helpers;
using BackgroundHandler.Infrastructure;
using Core.Helpers;
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
                
                await TryUpdateManifest(batchCompletionMessage, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling batch-completion message {MessageId}", message.MessageId);
            }
        }
        
        return false;
    }

    private async Task TryUpdateManifest(BatchCompletionMessage batchCompletionMessage, CancellationToken cancellationToken)
    {
        // Could this, in 1 operation, read + complete the batch + return whether there are others waiting?
        var batch = await dbContext.Batches.Include(b => b.Manifest)
            .ThenInclude(m => m.CanvasPaintings)
            .SingleOrDefaultAsync(b => b.Id == batchCompletionMessage.Id, cancellationToken);
        
        // batch isn't tracked by presentation, so nothing to do
        if (batch == null) return;

        var sw = Stopwatch.StartNew();
        
        // Other batches haven't completed, so no can't populate Manifest until all are complete
        if (await dbContext.Batches.AnyAsync(b => b.ManifestId == batch.ManifestId &&
                                                  b.Status != BatchStatus.Completed &&
                                                  b.Id != batch.Id, cancellationToken))
        {
            CompleteBatch(batch, batchCompletionMessage.Finished);
        }
        else
        {
            logger.LogInformation(
                "Attempting to complete assets in batch:{BatchId}, customer:{CustomerId}, manifest:{ManifestId}",
                batch.Id, batch.CustomerId, batch.ManifestId);

            try
            {
                CompleteBatch(batch, batchCompletionMessage.Finished);
                await manifestS3Manager.UpsertManifestFromStagingInStorage(batch.Manifest!, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error updating completing batch {BatchId} for manifest {ManifestId}", batch.Id,
                    batch.ManifestId);
                throw;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Updating batch:{BatchId}, customer:{CustomerId}, manifest:{ManifestId}. Completed in {Elapsed}ms",
            batch.Id, batch.CustomerId, batch.ManifestId, sw.ElapsedMilliseconds);
    }
    
    private static BatchCompletionMessage DeserializeMessage(QueueMessage message, ILogger logger)
    {
        try
        {
            return BatchCompletionMessage.FromQueueMessage(message);
        }
        catch (Exception)
        {
            logger.LogWarning("Could not deserialize message - attempting to deserialize using the old style format");
            throw;
        }
    }
    
    private static void CompleteBatch(Batch batch, DateTime finished)
    {
        batch.Processed = DateTime.UtcNow;
        batch.Finished = finished;
        batch.Status = BatchStatus.Completed;
    }
}
