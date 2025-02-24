using System.Text.Json;
using AWS.Helpers;
using AWS.SQS;
using BackgroundHandler.Helpers;
using Core.Helpers;
using Core.IIIF;
using DLCS.API;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
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

        var manifest = batch.Manifest;
        foreach (var canvasPainting in manifest.CanvasPaintings)
        {
            itemDictionary.TryGetValue(canvasPainting.AssetId!, out var item);
            
            if (item == null) continue;
            
            var thumbnailPath = item.Thumbnail?.OfType<Image>().GetThumbnailPath();
            
            canvasPainting.Thumbnail = thumbnailPath != null ? new Uri(thumbnailPath) : null;
            canvasPainting.Ingesting = false;
            canvasPainting.Modified = DateTime.UtcNow;

            if (canvasPainting is {StaticWidth: null, StaticHeight: null})
            {
                // #232: set static width/height if not provided.
                // if canvas painting comes with statics, use them
                // otherwise, if we can find dimensions within, use those
                // ReSharper disable once InvertIf
                if (GetCanvasDimensions(item) is var (width, height))
                {
                    canvasPainting.StaticWidth = width;
                    canvasPainting.StaticHeight = height;
                }
            }
            else if (canvasPainting is {StaticWidth: { } staticWidth, StaticHeight: { } staticHeight}
                     && item.Items?.GetFirstPaintingAnnotation()?.Body is Image image
                     && manifest.SpaceId.HasValue) // required for ImageRequest parsing and modification
            {
                // #232: if static_width/height provided in canvasPainting
                // then don't use ones from NQ
                // and set the body to those dimensions + resized id (image request uri)

                image.Width = staticWidth;
                image.Height = staticHeight;

                image.Id = pathGenerator.GetModifiedImageRequest(image.Id, manifest.CustomerId,
                    manifest.SpaceId.Value, staticWidth, staticHeight);
            }
        }
    }

    private static (int width, int height)? GetCanvasDimensions(Canvas canvas)
    {
        switch (canvas.GetFirstPaintingAnnotation()?.Body)
        {
            case null:
                // Just get from the services or from canvas itself as fallback
                if (GetItemDimensionsFromServices(canvas.Service) is { } canvasDimensions)
                    return canvasDimensions;
                return canvas is {Width: { } cWidth, Height: { } cHeight}
                    ? (cWidth, cHeight)
                    : null;

            case PaintingChoice choice:
                // Again, try first from services
                if (GetItemDimensionsFromServices(choice.Service) is { } choiceDimensions)
                    return choiceDimensions;

                // otherwise find like, first image with dimensions, if any
                return GetItemDimensionsFromImage(choice.Items?.OfType<Image>()
                    .FirstOrDefault(x => x is {Width: not null, Height: not null}));

            case Image image:
                return GetItemDimensionsFromImage(image);

            default: return null;
        }
    }

    private static (int width, int height)? GetItemDimensionsFromImage(Image? image) =>
        image switch
        {
            null => null,
            not null when GetItemDimensionsFromServices(image.Service) is { } imageDimensions => imageDimensions,
            {Width: { } iWidth, Height: { } iHeight} => (iWidth, iHeight),
            _ => null
        };

    private static (int width, int height)? GetItemDimensionsFromServices(IList<IService>? services)
    {
        if (services.IsNullOrEmpty())
            return null;

        if (services.OfType<ImageService3>().FirstOrDefault() is { } is3)
            return (is3.Width, is3.Height);

        if (services.OfType<ImageService2>().FirstOrDefault() is { } is2)
            return (is2.Width, is2.Height);

        return null;
    }
    
    private static BatchCompletionMessage DeserializeMessage(QueueMessage message)
    {
        var deserialized = JsonSerializer.Deserialize<BatchCompletionMessage>(message.Body, JsonSerializerOptions);
        return deserialized.ThrowIfNull(nameof(deserialized));
    }
}
