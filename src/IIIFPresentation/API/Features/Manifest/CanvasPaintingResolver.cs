using System.Data;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.IdGenerator;
using Core.Helpers;
using DLCS;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Models.Database;
using Models.DLCS;
using Repository.Manifests;
using CanvasPainting = Models.Database.CanvasPainting;
using DbManifest = Models.Database.Collections.Manifest;
using IIIFManifest = IIIF.Presentation.V3.Manifest;
using PresUpdateResult = API.Infrastructure.Requests.ModifyEntityResult<Models.API.Manifest.PresentationManifest, Models.API.General.ModifyCollectionType>;

namespace API.Features.Manifest;

public class CanvasPaintingResolver(
    IdentityManager identityManager,
    ManifestItemsParser manifestItemsParser,
    IOptions<DlcsSettings> dlcsOptions,
    ILogger<CanvasPaintingResolver> logger)
{
    private DlcsSettings settings = dlcsOptions.Value;
    
    /// <summary>
    /// Generate new CanvasPainting objects for items in provided <see cref="PresentationManifest"/>
    /// </summary>
    /// <returns>Tuple of either error OR newly created </returns>
    public async Task<(PresUpdateResult? updateResult, List<CanvasPainting>? canvasPaintings)> GenerateCanvasPaintings(
        int customerId, PresentationManifest presentationManifest, int? space, CancellationToken cancellationToken = default)
    {
        if (presentationManifest.PaintedResources.HasAsset())
        {
            return await CreateCanvasPaintingsFromAssets(customerId, presentationManifest, space!.Value,
                cancellationToken);
        }
        
        return await InsertCanvasPaintingsFromItems(customerId, presentationManifest, cancellationToken);
    }
    
    private async Task<(PresUpdateResult? error, List<CanvasPainting>? canvasPaintings)> InsertCanvasPaintingsFromItems(
        int customerId, IIIFManifest presentationManifest, CancellationToken cancellationToken)
    {
        var canvasPaintings = manifestItemsParser.ParseItemsToCanvasPainting(presentationManifest).ToList();
        var insertCanvasPaintingsError = await HandleInserts(canvasPaintings, customerId, cancellationToken);
        if (insertCanvasPaintingsError != null) return (insertCanvasPaintingsError, null);

        return (null, canvasPaintings);
    }

    /// <summary>
    /// Reconcile incoming manifest with any CanvasPainting objects already stored in DB. Resulting DB records should
    /// reflect incoming manifest.
    /// </summary>
    public async Task<PresUpdateResult?> UpdateCanvasPaintings(int customerId, IIIFManifest presentationManifest,
        DbManifest existingManifest, CancellationToken cancellationToken)
    {
        var incomingCanvasPaintings =
            manifestItemsParser.ParseItemsToCanvasPainting(presentationManifest).ToList();

        existingManifest.CanvasPaintings ??= [];

        // Iterate through all incoming - this is what we want to preserve in DB
        var processedCanvasPaintingIds = new List<int>(incomingCanvasPaintings.Count);
        var toInsert = new List<CanvasPainting>();
        foreach (var incoming in incomingCanvasPaintings)
        {
            CanvasPainting? matching = null;
            var candidates = existingManifest.CanvasPaintings
                .Where(cp => cp.CanvasOriginalId == incoming.CanvasOriginalId).ToList();
            if (candidates.Count == 1)
            {
                // Single item matching - check if we've processed it already. If so this is due to choice
                var potential = candidates.Single();
                if (!processedCanvasPaintingIds.Contains(potential.CanvasPaintingId))
                {
                    logger.LogTrace("Found existing canvas painting for {CanvasOriginalId}", incoming.CanvasOriginalId);
                    matching = potential;
                }
            }
            else if (candidates.Count > 1)
            {
                // If there are multiple matching items then Canvas is a choice
                logger.LogTrace("Found multiple canvas paintings for {CanvasOriginalId}", incoming.CanvasOriginalId);
                matching = candidates.SingleOrDefault(c => c.ChoiceOrder == incoming.ChoiceOrder);
            }

            if (matching == null)
            {
                if (incoming.ChoiceOrder.HasValue)
                {
                    // This is a choice. If there are other, existing items for the same canvas then seed canvas_id
                    incoming.Id = candidates.SingleOrDefault()?.Id;
                }

                // Store it in a list for processing later (e.g. for bulk generation of UniqueIds)
                logger.LogTrace("Adding canvas {CanvasIndex}, choice {ChoiceIndex}", incoming.CanvasOrder,
                    incoming.ChoiceOrder);
                toInsert.Add(incoming);
            }
            else
            {
                // Found matching DB record, update...
                logger.LogTrace("Updating canvasPaintingId {CanvasId}", matching.CanvasPaintingId);
                matching.UpdateFrom(incoming);
                processedCanvasPaintingIds.Add(matching.CanvasPaintingId);
            }
        }

        // Delete canvasPaintings from DB that are not in payload
        foreach (var toRemove in existingManifest.CanvasPaintings
                     .Where(cp => !processedCanvasPaintingIds.Contains(cp.CanvasPaintingId)).ToList())
        {
            logger.LogTrace("Deleting canvasPaintingId {CanvasId}", toRemove.CanvasPaintingId);
            existingManifest.CanvasPaintings.Remove(toRemove);
        }

        var insertCanvasPaintingsError = await HandleInserts(toInsert, customerId, cancellationToken);
        if (insertCanvasPaintingsError != null) return insertCanvasPaintingsError;
        existingManifest.CanvasPaintings.AddRange(toInsert);

        return null;
    }

    private async Task<PresUpdateResult?> HandleInserts(List<CanvasPainting> canvasPaintings, int customerId,
        CancellationToken cancellationToken)
    {
        if (canvasPaintings.IsNullOrEmpty()) return null;

        logger.LogTrace("Adding {CanvasCounts} to Manifest", canvasPaintings.Count);
        var requiredIds = canvasPaintings
            .Where(cp => string.IsNullOrEmpty(cp.Id))
            .DistinctBy(c => c.CanvasOrder)
            .Count();
        var canvasPaintingIds = await GenerateUniqueCanvasPaintingIds(requiredIds, customerId, cancellationToken);
        if (canvasPaintingIds == null) return ErrorHelper.CannotGenerateUniqueId<PresentationManifest>();

        // Build a dictionary of canvas_order:canvas_id, this is populated as we iterate over canvas paintings.
        // We will also seed it with any 'new' items that are actually new Choices as these will have been prepopulated
        // with a canvas_id
        var canvasIds = canvasPaintings
            .Where(cp => !string.IsNullOrEmpty(cp.Id))
            .ToDictionary(k => k.CanvasOrder, v => v.Id);
        var count = 0;
        foreach (var cp in canvasPaintings)
        {
            // CanvasPainting records that have the same CanvasOrder will share the same CanvasId
            if (canvasIds.TryGetValue(cp.CanvasOrder, out var canvasOrderId))
            {
                cp.Id = canvasOrderId;
                continue;
            }

            // If item has an Id, it's an update for a Choice so use the existing canvas_id. Else grab a new one
            var canvasId = string.IsNullOrEmpty(cp.Id) ? canvasPaintingIds[count++] : cp.Id;
            canvasIds[cp.CanvasOrder] = canvasId;
            cp.Id = canvasId;
        }

        return null;
    }

    private async Task<IList<string>?> GenerateUniqueCanvasPaintingIds(int count, int customerId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (count == 0) return [];
            
            return await identityManager.GenerateUniqueIds<CanvasPainting>(customerId, count,
                cancellationToken);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "Unable to generate {Count} unique CanvasPainting ids for customer {CustomerId}",
                count, customerId);
            return null;
        }
    }
    
    private async Task<(PresUpdateResult? updateResult, List<CanvasPainting>? canvasPaintings)> CreateCanvasPaintingsFromAssets(
        int customerId, PresentationManifest presentationManifest, int manifestSpace, CancellationToken cancellationToken)
    {
        var paintedResourceCount = presentationManifest.PaintedResources!.Count;

        var canvasPaintingIds =
            await GenerateUniqueCanvasPaintingIds(paintedResourceCount, customerId, cancellationToken);
        if (canvasPaintingIds == null)
            return (ErrorHelper.CannotGenerateUniqueId<PresentationManifest>(), null);
        
        var canvasPaintings = new List<CanvasPainting>();
        var count = 0;
        foreach (var paintedResource in presentationManifest.PaintedResources)
        {
            if (paintedResource.Asset == null) continue;
    
            if (!paintedResource.Asset.TryGetValue("space", out var space))
            {
                paintedResource.Asset.Add("space", manifestSpace);
                space = manifestSpace;
            }

            if (!paintedResource.Asset.TryGetValue("id", out var id))
            {
                return (ErrorHelper.CouldNotRetrieveAssetId<PresentationManifest>(), null);
            }

            var cp = new CanvasPainting
            {
                Id = canvasPaintingIds[count],
                Label = paintedResource.CanvasPainting?.Label,
                Created = DateTime.UtcNow,
                CustomerId = customerId,
                CanvasOrder = paintedResource.CanvasPainting?.CanvasOrder ?? count,
                AssetId = AssetId.FromString($"{customerId}/{space}/{id}"),
                ChoiceOrder = paintedResource.CanvasPainting?.ChoiceOrder ?? -1,
                Ingesting = true
            };

            count++;
            canvasPaintings.Add(cp);
        }
        return (null, canvasPaintings);
    }
}
