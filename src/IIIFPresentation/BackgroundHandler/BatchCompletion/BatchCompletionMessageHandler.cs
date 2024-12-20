using System.Text.Json;
using AWS.SQS;
using BackgroundHandler.Helpers;
using Core.Helpers;
using DLCS;
using DLCS.API;
using DLCS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.Database.General;
using Repository;
using Batch = Models.Database.General.Batch;

namespace BackgroundHandler.BatchCompletion;

public class BatchCompletionMessageHandler(
    PresentationContext dbContext,
    IDlcsApiClient dlcsApiClient,
    IOptions<DlcsSettings> dlcsOptions,
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
            .FirstOrDefaultAsync(b => b.Id == batchCompletionMessage.Id, cancellationToken);
        
        // batch isn't tracked by presentation, so nothing to do
        if (batch == null) return;

        // Other batches haven't completed, so no point populating items until all are complete
        if (await dbContext.Batches.AnyAsync(b => b.ManifestId == batch.ManifestId && b.Status != BatchStatus.Completed,
                cancellationToken))
        {
            return;
        }

        logger.LogInformation(
            "Attempting to complete assets in batch {BatchId} for customer {CustomerId} with the manifest {ManifestId}",
            batch.Id, batch.CustomerId, batch.ManifestId);

        var assets = await RetrieveImages(batch, cancellationToken);
        
        UpdateCanvasPaintings(assets, batch);
        CompleteBatch(batch);

        await dbContext.SaveChangesAsync(cancellationToken);
        
        logger.LogTrace("updating batch {BatchId} has been completed", batch.Id);
    }

    private void CompleteBatch(Batch batch)
    {
        batch.Processed = DateTime.UtcNow;
        batch.Status = BatchStatus.Completed;
    }

    private void UpdateCanvasPaintings(HydraCollection<Asset> assets, Batch batch)
    {
        if (batch.Manifest?.CanvasPaintings == null) return;
        
        foreach (var canvasPainting in batch.Manifest.CanvasPaintings)
        {
            if (canvasPainting.Ingesting)
            {
                var assetId = AssetId.FromString(canvasPainting.AssetId!);
                
                var asset = assets.Members.FirstOrDefault(a => a.ResourceId!.Contains($"{assetId.Space}/images/{assetId.Asset}"));
                if (asset == null || asset.Ingesting) continue;
                
                canvasPainting.CanvasOriginalId =
                    new Uri(
                        $"{dlcsSettings.OrchestratorUri}/iiif-img/{assetId.Customer}/{assetId.Space}/{assetId.Asset}/full/max/0/default.jpg"); //todo: do we need this? Supposed to be null for an asset really
                canvasPainting.Thumbnail =
                    new Uri(
                        $"{dlcsSettings.OrchestratorUri}/thumbs/{assetId.Customer}/{assetId.Space}/{assetId.Asset}/100,/max/0/default.jpg"); //todo: how to get this?
                canvasPainting.Ingesting = false;
                canvasPainting.Modified = DateTime.UtcNow;
                canvasPainting.StaticHeight = asset.Height;
                canvasPainting.StaticWidth = asset.Width;
            }
        }
    }

    private async Task<HydraCollection<Asset>> RetrieveImages(Batch batch, CancellationToken cancellationToken)
    {
        var assetsRequest =
            batch.Manifest?.CanvasPaintings?.Where(c => c.AssetId != null).Select(c => c.AssetId!).ToList() ?? [];
        
        return await dlcsApiClient.RetrieveAllImages(batch.CustomerId, assetsRequest, cancellationToken);
    }

    private static BatchCompletionMessage DeserializeMessage(QueueMessage message)
    {
        var deserialized = JsonSerializer.Deserialize<BatchCompletionMessage>(message.Body, JsonSerializerOptions);
        return deserialized.ThrowIfNull(nameof(deserialized));
    }
}
