using System.Diagnostics;
using System.Text.Json;
using AWS.Helpers;
using AWS.SQS;
using BackgroundHandler.Helpers;
using Core.Helpers;
using Core.IIIF;
using DLCS.API;
using IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using Microsoft.EntityFrameworkCore;
using Models.Database.General;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Repository.Paths;
using Batch = Models.Database.General.Batch;

namespace BackgroundHandler.BatchCompletion;

public class BatchCompletionMessageHandler(
    PresentationContext dbContext,
    IDlcsOrchestratorClient dlcsOrchestratorClient,
    IIIIFS3Service iiifS3,
    IPathGenerator pathGenerator,
    IManifestMerger manifestMerger,
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
            CompleteBatch(batch, batchCompletionMessage.Finished, false);
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
                CompleteBatch(batch, batchCompletionMessage.Finished, true);
                await UpdateManifestInS3(namedQueryManifest, batch, cancellationToken);
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
    
    private async Task UpdateManifestInS3(Manifest? namedQueryManifest, Batch batch, CancellationToken cancellationToken)
    {
        var dbManifest = batch.Manifest!;
        var manifest = await iiifS3.ReadIIIFFromS3<Manifest>(dbManifest, true, cancellationToken);

        var mergedManifest = manifestMerger.ProcessCanvasPaintings(
            manifest.ThrowIfNull(nameof(manifest), "Manifest was not found in staging location"),
            namedQueryManifest,
            dbManifest.CanvasPaintings);

        await iiifS3.SaveIIIFToS3(mergedManifest, dbManifest, pathGenerator.GenerateFlatManifestId(dbManifest),
            false, cancellationToken);

        await iiifS3.DeleteIIIFFromS3(dbManifest, true);
    }

    private static void CompleteBatch(Batch batch, DateTime finished, bool finalBatch)
    {
        var processed = DateTime.UtcNow;
        
        batch.Processed = processed;
        batch.Finished = finished;
        batch.Status = BatchStatus.Completed;

        if (finalBatch)
        {
            batch.Manifest!.LastProcessed = processed;
        }
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
