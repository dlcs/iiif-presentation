using System.Diagnostics;
using System.Text.Json;
using AWS.SQS;
using BackgroundHandler.Helpers;
using Core.Helpers;
using DLCS.API;
using Microsoft.EntityFrameworkCore;
using Models.Database.General;
using Repository;
using Services.Manifests.AWS;
using Services.Manifests.Database;

namespace BackgroundHandler.BatchCompletion;

public class BatchCompletionMessageHandler(
    PresentationContext dbContext,
    IDlcsOrchestratorClient dlcsOrchestratorClient,
    IManifestS3Manager manifestS3Manager,
    IManifestDatabaseManager manifestDatabaseManager,
    ILogger<BatchCompletionMessageHandler> logger)
    : IMessageHandler
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        using (LogContextHelpers.SetServiceName(nameof(BatchCompletionMessageHandler), message.MessageId))
        {
            try
            {
                var batchCompletionMessage = DeserializeMessage(message, logger);

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
        var batch = await dbContext.Batches.Include(b => b.Manifest)
            .ThenInclude(m => m.CanvasPaintings)
            .SingleOrDefaultAsync(b => b.Id == batchCompletionMessage.Id, cancellationToken);
        
        // batch isn't tracked by presentation, so nothing to do
        if (batch == null) return;

        var sw = Stopwatch.StartNew();
        
        // Other batches haven't completed, so no point populating items until all are complete
        if (await dbContext.Batches.AnyAsync(b => b.ManifestId == batch.ManifestId &&
                                                  b.Status != BatchStatus.Completed &&
                                                  b.Id != batch.Id, cancellationToken))
        {
            manifestDatabaseManager.CompleteBatch(batch, batchCompletionMessage.Finished, false);
        }
        else
        {
            logger.LogInformation(
                "Attempting to complete assets in batch:{BatchId}, customer:{CustomerId}, manifest:{ManifestId}",
                batch.Id, batch.CustomerId, batch.ManifestId);

            var batches = dbContext.Batches.Where(b => b.ManifestId == batch.ManifestId).Select(b => b.Id).ToList();

            var namedQueryManifest =
                await dlcsOrchestratorClient.RetrieveAssetsForManifest(batch.CustomerId, batches,
                    cancellationToken);

            try
            {
                manifestDatabaseManager.CompleteBatch(batch, batchCompletionMessage.Finished, true);
                await manifestS3Manager.UpdateManifestInS3(namedQueryManifest, batch.Manifest!, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error updating completing batch {BatchId} for manifest {ManifestId}", batch.Id,
                    namedQueryManifest.Id);
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
        BatchCompletionMessage? deserializedBatchCompletionMessage;
        
        try
        {
            deserializedBatchCompletionMessage =
                JsonSerializer.Deserialize<BatchCompletionMessage>(message.Body, JsonSerializerOptions);
        }
        catch (Exception)
        {
            logger.LogWarning("Could not deserialize message - attempting to deserialize using the old style format");
            var deserialized = JsonSerializer.Deserialize<OldBatchCompletionMessage>(message.Body, JsonSerializerOptions);
            deserializedBatchCompletionMessage = deserialized?.ConvertBatchCompletionMessage();
        }
        
        return deserializedBatchCompletionMessage.ThrowIfNull(nameof(deserializedBatchCompletionMessage));
    }
}
