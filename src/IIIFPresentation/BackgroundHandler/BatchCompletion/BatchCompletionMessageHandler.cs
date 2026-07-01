using System.Diagnostics;
using AWS.SQS;
using BackgroundHandler.Infrastructure;
using Core.Helpers;
using Microsoft.EntityFrameworkCore;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Services.Manifests;
using Services.Manifests.AWS;
using Manifest = Models.Database.Collections.Manifest;

namespace BackgroundHandler.BatchCompletion;

public class BatchCompletionMessageHandler(
    PresentationContext dbContext,
    ICustomerIdProvider customerIdProvider,
    IManifestStorageManager manifestS3Manager,
    IDlcsManifestMerger dlcsManifestMerger,
    ILogger<BatchCompletionMessageHandler> logger)
    : MessageHandlerBase<BatchCompletionMessage>(logger)
{
    protected override BatchCompletionMessage DeserializeMessage(QueueMessage message) =>
        BatchCompletionMessage.FromQueueMessage(message);

    protected override async Task<bool> HandleMessage(BatchCompletionMessage batchCompletionMessage,
        QueueMessage rawMessage, CancellationToken cancellationToken)
    {
        customerIdProvider.SetCustomerId(batchCompletionMessage.Customer);
        return await TryUpdateManifest(batchCompletionMessage, rawMessage.ApproximateReceiveCount, cancellationToken);
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
            return DiscardUntrackedResource(approximateReceiveCount, $"Batch {batchCompletionMessage.Id}");
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
            Logger.LogInformation(
                "Attempting to complete assets in batch:{BatchId}, customer:{CustomerId}, manifest:{ManifestId}",
                batch.Id, batch.CustomerId, batch.ManifestId);

            try
            {
                if (TryCompleteBatch(batch, batchCompletionMessage.Finished))
                {
                    await CompleteManifestFromStaging(batch.Manifest!, cancellationToken);
                }
                else
                {
                    Logger.LogInformation(
                        "Batch:{BatchId}, customer:{CustomerId}, manifest:{ManifestId} already completed",
                        batch.Id, batch.CustomerId, batch.ManifestId);
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error updating completing batch {BatchId} for manifest {ManifestId}", batch.Id,
                    batch.ManifestId);
                return false;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation(
            "Updating batch:{BatchId}, customer:{CustomerId}, manifest:{ManifestId}. Completed in {Elapsed}ms",
            batch.Id, batch.CustomerId, batch.ManifestId, sw.ElapsedMilliseconds);
        return true;
    }

    // Read the staged manifest, merge in the DLCS content, save the final manifest (promoting any stored original
    // payload) then remove the staging artifacts.
    private async Task CompleteManifestFromStaging(Manifest dbManifest, CancellationToken cancellationToken)
    {
        var staged = await manifestS3Manager.ReadStagedManifest(dbManifest, cancellationToken);
        staged.Manifest.ThrowIfNull(nameof(staged.Manifest), "Manifest was not found in staging location");

        var merged = await dlcsManifestMerger.Augment(staged.Manifest!, dbManifest, cancellationToken);
        await manifestS3Manager.SaveManifestInStorage(merged, dbManifest, staged.Original, saveToStaging: false,
            cancellationToken);
        await manifestS3Manager.DeleteStagedManifest(dbManifest);
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
