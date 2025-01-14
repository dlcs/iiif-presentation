using System.Text.Json;
using AWS.Helpers;
using AWS.SQS;
using BackgroundHandler.Helpers;
using Core.Helpers;
using Core.IIIF;
using DLCS;
using DLCS.API;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.Database.General;
using Models.DLCS;
using Repository;
using Batch = Models.Database.General.Batch;

namespace BackgroundHandler.BatchCompletion;

public class BatchCompletionMessageHandler(
    PresentationContext dbContext,
    IDlcsOrchestratorClient dlcsOrchestratorClient,
    IOptions<DlcsSettings> dlcsOptions,
    IIIIFS3Service iiifS3,
    ILogger<BatchCompletionMessageHandler> logger)
    : IMessageHandler
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly DlcsSettings dlcsSettings = dlcsOptions.Value;

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
            .SingleOrDefaultAsync(b => b.Id == batchCompletionMessage.Id, cancellationToken);
        
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

            var batches = dbContext.Batches.Where(b => b.ManifestId == batch.ManifestId).Select(b => b.Id).ToList();

            var generatedManifest =
                await dlcsOrchestratorClient.RetrieveAssetsForManifest(batch.CustomerId, batches,
                    cancellationToken);
            
            Dictionary<AssetId, Canvas> itemDictionary;
            
            try
            {
                itemDictionary = generatedManifest.Items.Select(i =>
                        new KeyValuePair<AssetId, Canvas>(
                            AssetId.FromString(i.Id!.Remove(i.Id.IndexOf("/canvas", StringComparison.Ordinal))
                                .Remove(0, $"{dlcsSettings.OrchestratorUri!.ToString()}/iii-img/".Length + 1)), i))
                    .ToDictionary();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error retrieving the canvas id from an item in {ManifestId}", generatedManifest?.Id);
                throw;
            }

            UpdateCanvasPaintings(generatedManifest, batch, itemDictionary!);
            CompleteBatch(batch, batchCompletionMessage.Finished);
            await UpdateManifestInS3(generatedManifest.Thumbnail, itemDictionary, batch, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogTrace("updating batch {BatchId} has been completed", batch.Id);
    }

    private async Task UpdateManifestInS3(List<ExternalResource>? thumbnail, Dictionary<AssetId, Canvas> itemDictionary, Batch batch, 
        CancellationToken cancellationToken = default)
    {
        var manifest = await iiifS3.ReadIIIFFromS3<Manifest>(batch.Manifest!, cancellationToken);

        var mergedManifest = ManifestMerger.Merge(manifest.ThrowIfNull(nameof(manifest)),
            batch.Manifest?.CanvasPaintings, itemDictionary, thumbnail);
        
        await iiifS3.SaveIIIFToS3(mergedManifest, batch.Manifest!, "", cancellationToken);
    }

    private void CompleteBatch(Batch batch, DateTime finished)
    {
        batch.Processed = DateTime.UtcNow;
        batch.Finished = finished;
        batch.Status = BatchStatus.Completed;
    }

    private void UpdateCanvasPaintings(Manifest generatedManifest, Batch batch, Dictionary<AssetId, Canvas> itemDictionary)
    {
        if (batch.Manifest?.CanvasPaintings == null)
        {
            logger.LogWarning(
                "Received a batch completion notification with no canvas paintings on the batch {BatchId}", batch.Id);
            return;
        }
        
        foreach (var canvasPainting in batch.Manifest.CanvasPaintings)
        {
            itemDictionary.TryGetValue(canvasPainting.AssetId!, out var item);
            
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
