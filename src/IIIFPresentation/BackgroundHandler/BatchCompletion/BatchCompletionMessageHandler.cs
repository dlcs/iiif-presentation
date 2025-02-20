﻿using System.Text.Json;
using AWS.Helpers;
using AWS.SQS;
using BackgroundHandler.Helpers;
using Core.Helpers;
using Core.IIIF;
using DLCS.API;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using Microsoft.EntityFrameworkCore;
using Models.Database.General;
using Models.DLCS;
using Repository;
using Repository.Paths;
using Batch = Models.Database.General.Batch;

namespace BackgroundHandler.BatchCompletion;

public class BatchCompletionMessageHandler(
    PresentationContext dbContext,
    IDlcsOrchestratorClient dlcsOrchestratorClient,
    IIIIFS3Service iiifS3,
    IPathGenerator pathGenerator,
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
                "Attempting to complete assets in batch {BatchId} for customer {CustomerId} with the manifest {ManifestId}",
                batch.Id, batch.CustomerId, batch.ManifestId);

            var batches = dbContext.Batches.Where(b => b.ManifestId == batch.ManifestId).Select(b => b.Id).ToList();

            var namedQueryManifest =
                await dlcsOrchestratorClient.RetrieveAssetsForManifest(batch.CustomerId, batches,
                    cancellationToken);
            
            var itemDictionary = BuildAssetIdToCanvasLookup(namedQueryManifest, batch.Manifest!);

            try
            {
                UpdateCanvasPaintings(batch, itemDictionary);
                CompleteBatch(batch, batchCompletionMessage.Finished, true);
                await UpdateManifestInS3(itemDictionary, batch, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error updating completing batch {BatchId} for manifest {ManifestId}", batch.Id,
                    namedQueryManifest.Id);
                throw;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogTrace("updating batch {BatchId} has been completed", batch.Id);
    }

    private Dictionary<AssetId, Canvas> BuildAssetIdToCanvasLookup(Manifest? namedQueryManifest, 
        Models.Database.Collections.Manifest dbManifest)
    {
        if (namedQueryManifest?.Items == null)
        {
            logger.LogWarning("NamedQuery Manifest '{ManifestId}' null or missing items",
                namedQueryManifest?.Id ?? "no-id");
            throw new ArgumentNullException(nameof(namedQueryManifest));
        }

        try
        {
            var canvasPaintings = dbManifest.CanvasPaintings;
            var canvases = namedQueryManifest.Items;
            var finalDictionary = new Dictionary<AssetId, Canvas>(canvases!.Count);
            foreach (var canvas in canvases)
            {
                var assetId = canvas.GetAssetIdFromNamedQueryCanvasId();
                var canvasPaintingForAsset = canvasPaintings.FirstOrDefault(cp => cp.AssetId == assetId);
                if (canvasPaintingForAsset == null)
                {
                    logger.LogWarning("Unable to find CanvasPainting record for manifest {CustomerId}:{ManifestId}",
                        dbManifest.CustomerId, dbManifest.Id);
                    continue;
                }

                // Set all NQ Canvas ids to those that we use in iiif-presentation
                logger.LogDebug("Rewriting NQ ids for {CanvasId}, from manifest {CustomerId}:{ManifestId}",
                    canvas.Id, dbManifest.CustomerId, dbManifest.Id);
                canvas.Id = pathGenerator.GenerateCanvasId(canvasPaintingForAsset);
                canvas.GetFirstAnnotationPage()!.Id = pathGenerator.GenerateAnnotationPagesId(canvasPaintingForAsset);
                canvas.GetFirstPaintingAnnotation()!.Id = pathGenerator.GeneratePaintingAnnotationId(canvasPaintingForAsset);
                finalDictionary[assetId] = canvas;
            }
            return finalDictionary;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error building Asset:Canvas lookup for {ManifestId}", namedQueryManifest?.Id);
            throw;
        }
    }

    private async Task UpdateManifestInS3(Dictionary<AssetId, Canvas> itemDictionary, Batch batch, 
        CancellationToken cancellationToken = default)
    {
        var dbManifest = batch.Manifest!;
        var manifest = await iiifS3.ReadIIIFFromS3<Manifest>(dbManifest, cancellationToken);

        var mergedManifest = ManifestMerger.Merge(manifest.ThrowIfNull(nameof(manifest)),
            batch.Manifest?.CanvasPaintings, itemDictionary);

        await iiifS3.SaveIIIFToS3(mergedManifest, dbManifest, pathGenerator.GenerateFlatManifestId(dbManifest),
            cancellationToken);
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

    private void UpdateCanvasPaintings(Batch batch, Dictionary<AssetId, Canvas> itemDictionary)
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
