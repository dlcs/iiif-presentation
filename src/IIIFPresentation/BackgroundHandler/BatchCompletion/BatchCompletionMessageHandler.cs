using System.Text.Json;
using AWS.Helpers;
using AWS.SQS;
using BackgroundHandler.Helpers;
using Core.Helpers;
using Core.IIIF;
using DLCS;
using DLCS.API;
using DLCS.Models;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.Database.General;
using Repository;
using Batch = Models.Database.General.Batch;

namespace BackgroundHandler.BatchCompletion;

public class BatchCompletionMessageHandler(
    PresentationContext dbContext,
    IDlcsOrchestratorClient dlcsOrchestratorClient,
    IIIIFS3Service iiifS3,
    ILogger<BatchCompletionMessageHandler> logger)
    : IMessageHandler
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        using (LogContextHelpers.SetServiceName(nameof(BatchCompletionMessageHandler)))
        {
            try
            {
                var batchCompletionMessage = DeserializeMessage(message);

                await UpdateAssetsIfRequired(batchCompletionMessage, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling batch-completion message {MessageId}", message.MessageId);
            }
        }
        
        return false;
    }

    private async Task UpdateAssetsIfRequired(BatchCompletionMessage batchCompletionMessage, CancellationToken cancellationToken)
    {
        var batch = await dbContext.Batches.Include(b => b.Manifest)
            .ThenInclude(m => m.CanvasPaintings)
            .FirstOrDefaultAsync(b => b.Id == batchCompletionMessage.Id, cancellationToken);
        
        // batch isn't tracked by presentation, so nothing to do
        if (batch == null) return;
        
            // Other batches haven't completed, so no point populating items until all are complete
            if (await dbContext.Batches.AnyAsync(b => b.ManifestId == batch.ManifestId &&
                                                      b.Status != BatchStatus.Completed &&
                                                      b.Id != batch.Id, cancellationToken))
            {
                CompleteBatch(batch, batchCompletionMessage.Finished);
            }
            else
            {
                logger.LogInformation(
                    "Attempting to complete assets in batch {BatchId} for customer {CustomerId} with the manifest {ManifestId}",
                    batch.Id, batch.CustomerId, batch.ManifestId);

                var generatedManifest =
                    await dlcsOrchestratorClient.RetrieveImagesForManifest(batch.CustomerId, batch.ManifestId!,
                        cancellationToken);

                UpdateCanvasPaintings(generatedManifest, batch);
                CompleteBatch(batch, batchCompletionMessage.Finished);
                await UpdateManifestInS3(generatedManifest, batch, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogTrace("updating batch {BatchId} has been completed", batch.Id);
    }

    private async Task UpdateManifestInS3(Manifest generatedManifest, Batch batch, 
        CancellationToken cancellationToken = default)
    {
        var manifest = await iiifS3.ReadIIIFFromS3<Manifest>(batch.Manifest!, cancellationToken);
        
        var mergedManifest = ManifestMerger.Merge(manifest, generatedManifest, batch.Manifest?.CanvasPaintings);
        manifest.ThrowIfNull("Failed to retrieve manifest");
        
        await iiifS3.SaveIIIFToS3(mergedManifest, batch.Manifest, "", cancellationToken);
    }

    private void CompleteBatch(Batch batch, DateTime finished)
    {
        batch.Processed = finished;
        batch.Status = BatchStatus.Completed;
    }

    private void UpdateCanvasPaintings(Manifest generatedManifest, Batch batch)
    {
        if (batch.Manifest?.CanvasPaintings == null) return;
        
        foreach (var canvasPainting in batch.Manifest.CanvasPaintings)
        {
            var assetId = AssetId.FromString(canvasPainting.AssetId!);

            var item = generatedManifest.Items?.FirstOrDefault(i => i.Id!.Contains(assetId.ToString()));
            
            if (item == null) continue;

            var thumbnailPath = item.Thumbnail?.OfType<Image>().GetThumbnailPath();
            
            canvasPainting.Thumbnail = thumbnailPath != null ? new Uri(thumbnailPath) : null;
            canvasPainting.Ingesting = false;
            canvasPainting.Modified = DateTime.UtcNow;
            canvasPainting.StaticHeight = item.Height;
            canvasPainting.StaticWidth = item.Width;
        }
        
    }

    private static BatchCompletionMessage DeserializeMessage(QueueMessage message)
    {
        var deserialized = JsonSerializer.Deserialize<BatchCompletionMessage>(message.Body, JsonSerializerOptions);
        return deserialized.ThrowIfNull(nameof(deserialized));
    }
}
