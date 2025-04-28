using System.Data;
using System.Diagnostics;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.IdGenerator;
using Core.Helpers;
using DLCS.Models;
using Models.API.Manifest;
using Models.Database;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository.Manifests;
using Repository.Paths;
using CanvasPainting = Models.Database.CanvasPainting;
using DbManifest = Models.Database.Collections.Manifest;
using IIIFManifest = IIIF.Presentation.V3.Manifest;
using PresUpdateResult = API.Infrastructure.Requests.ModifyEntityResult<Models.API.Manifest.PresentationManifest, Models.API.General.ModifyCollectionType>;

namespace API.Features.Manifest;

public class CanvasPaintingResolver(
    IdentityManager identityManager,
    ManifestItemsParser manifestItemsParser,
    ILogger<CanvasPaintingResolver> logger)
{
    /// <summary>
    /// Generate new CanvasPainting objects for items in provided <see cref="PresentationManifest"/>
    /// </summary>
    /// <returns>Tuple of either error OR newly created </returns>
    public async Task<(PresUpdateResult? updateResult, List<CanvasPainting>? canvasPaintings)> GenerateCanvasPaintings(
        int customerId, PresentationManifest presentationManifest, CancellationToken cancellationToken = default)
    {
        if (presentationManifest.PaintedResources.HasAsset())
        {
            return await CreateCanvasPaintingsFromAssets(customerId, presentationManifest, cancellationToken);
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
    /// Generate new CanvasPainting objects for items in provided <see cref="PresentationManifest"/>
    /// </summary>
    /// <returns>A presentation update result</returns>
    public async Task<PresUpdateResult?>  UpdateCanvasPaintings(int customerId, PresentationManifest presentationManifest,
        DbManifest existingManifest, CancellationToken cancellationToken = default)
    {
        if (presentationManifest.PaintedResources.HasAsset())
        {
            return await UpdateCanvasPaintingsFromAssets(customerId, presentationManifest, existingManifest, cancellationToken);
        }
        
        return await UpdateCanvasPaintingsFromItems(customerId, presentationManifest, existingManifest, cancellationToken);
    }

    private async Task<PresUpdateResult?> UpdateCanvasPaintingsFromAssets(int customerId, 
        PresentationManifest presentationManifest, DbManifest existingManifest, CancellationToken cancellationToken)
    {
        var (updateResult, incomingCanvasPaintings) =
            GeneratePartialCanvasPaintingsFromAssets(customerId, presentationManifest);

        existingManifest.CanvasPaintings ??= [];

        if (updateResult != null) return updateResult;

        Debug.Assert(incomingCanvasPaintings is not null, "incomingCanvasPaintings is not null");

        var toInsert = UpdateCanvasPaintingRecords(existingManifest.CanvasPaintings, incomingCanvasPaintings);
        
        var insertCanvasPaintingsError = await HandleInserts(toInsert, customerId, cancellationToken);
        if (insertCanvasPaintingsError != null) return insertCanvasPaintingsError;
        existingManifest.CanvasPaintings.AddRange(toInsert);
        existingManifest.LastProcessed = DateTime.UtcNow;
        
        return null;
    }

    /// <summary>
    /// Reconcile incoming manifest with any CanvasPainting objects already stored in DB. Resulting DB records should
    /// reflect incoming manifest.
    /// </summary>
    private async Task<PresUpdateResult?> UpdateCanvasPaintingsFromItems(int customerId, IIIFManifest presentationManifest,
        DbManifest existingManifest, CancellationToken cancellationToken)
    {
        var incomingCanvasPaintings =
            manifestItemsParser.ParseItemsToCanvasPainting(presentationManifest).ToList();

        existingManifest.CanvasPaintings ??= [];

        var toInsert = UpdateCanvasPaintingRecords(existingManifest.CanvasPaintings, incomingCanvasPaintings);

        var insertCanvasPaintingsError = await HandleInserts(toInsert, customerId, cancellationToken);
        if (insertCanvasPaintingsError != null) return insertCanvasPaintingsError;
        existingManifest.CanvasPaintings.AddRange(toInsert);

        return null;
    }

    private List<CanvasPainting> UpdateCanvasPaintingRecords(List<CanvasPainting> existingCanvasPaintings, 
        List<CanvasPainting> incomingCanvasPaintings)
    {
        var processedCanvasPaintingIds = new List<int>(incomingCanvasPaintings.Count);
        var toInsert = new List<CanvasPainting>();
        
        foreach (var incoming in incomingCanvasPaintings)
        {
            UpdateCanvasPainting(existingCanvasPaintings, incoming, processedCanvasPaintingIds, toInsert);
        }
        
        // Delete canvasPaintings from DB that are not in payload
        foreach (var toRemove in existingCanvasPaintings
                     .Where(cp => !processedCanvasPaintingIds.Contains(cp.CanvasPaintingId)).ToList())
        {
            logger.LogTrace("Deleting canvasPaintingId {CanvasId}", toRemove.CanvasPaintingId);
            existingCanvasPaintings.Remove(toRemove);
        }

        return toInsert;
    }

    private void UpdateCanvasPainting(List<CanvasPainting> existingCanvasPaintings, CanvasPainting incoming,
        List<int> processedCanvasPaintingIds, List<CanvasPainting> toInsert)
    {
        CanvasPainting? matching = null;

        var candidates = (incoming.Id is {Length: > 0} incomingId
                // match by provided canvas id
                ? existingCanvasPaintings.Where(cp => cp.Id == incomingId)
                // match by original canvas id (if present, otherwise empty list)
                : existingCanvasPaintings.Where(cp => cp.CanvasOriginalId == incoming.CanvasOriginalId)
            ).ToList();

        var canvasLoggingId = incoming.CanvasOriginalId?.ToString() ?? incoming.Id;

        switch (candidates.Count)
        {
            case 1:
            {
                // Single item matching - check if we've processed it already. If so this is due to choice
                var potential = candidates.Single();
                if (!processedCanvasPaintingIds.Contains(potential.CanvasPaintingId))
                {
                    logger.LogTrace("Found existing canvas painting for {CanvasLoggingId}", canvasLoggingId);
                    matching = potential;
                }

                break;
            }
            case > 1:
                // If there are multiple matching items then Canvas is a choice
                logger.LogTrace("Found multiple canvas paintings for {CanvasLoggingId}", canvasLoggingId);
                matching = candidates.SingleOrDefault(c => c.ChoiceOrder == incoming.ChoiceOrder);
                break;
        }

        if (matching == null)
        {
            if (incoming.ChoiceOrder.HasValue
                && candidates.SingleOrDefault()?.Id is {Length: > 0} existingId)
                // This is a choice. If there are other, existing items for the same canvas, then seed canvas_id
                incoming.Id = existingId;

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

    private async Task<PresUpdateResult?> HandleInserts(List<CanvasPainting> canvasPaintings, int customerId,
        CancellationToken cancellationToken)
    {
        if (canvasPaintings.IsNullOrEmpty()) return null;

        logger.LogTrace("Adding {CanvasCounts} to Manifest", canvasPaintings.Count);
        var requiredIds = canvasPaintings.GetRequiredNumberOfCanvases();
        var canvasPaintingIds = await GenerateUniqueCanvasPaintingIds(requiredIds, customerId, cancellationToken);
        if (canvasPaintingIds == null) return ErrorHelper.CannotGenerateUniqueId<PresentationManifest>();

        // Build a dictionary of canvas_order:canvas_id, this is populated as we iterate over canvas paintings.
        // We will also seed it with any 'new' items that are actually new Choices as these will have been prepopulated
        // with a canvas_id
        var canvasIds = canvasPaintings
            .Where(cp => !string.IsNullOrEmpty(cp.Id))
            .GroupBy(cp => cp.CanvasOrder) // grouping by canvas order avoids issues with choices providing duplicate canvas ids
            .ToDictionary(k => k.Key, v => v.First().Id); // the id will be the same in all items within a choice construct
        foreach (var cp in canvasPaintings)
        {
            // CanvasPainting records that have the same CanvasOrder will share the same CanvasId
            if (canvasIds.TryGetValue(cp.CanvasOrder, out var canvasOrderId))
            {
                cp.Id = canvasOrderId;
                continue;
            }

            // If item has an Id, it's an update for a Choice so use the existing canvas_id. Else grab a new one
            var canvasId = string.IsNullOrEmpty(cp.Id) ? canvasPaintingIds.Pop() : cp.Id;
            canvasIds[cp.CanvasOrder] = canvasId;
            cp.Id = canvasId;
        }

        return null;
    }

    private async Task<Stack<string>?> GenerateUniqueCanvasPaintingIds(int count, int customerId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (count == 0) return [];
            
            var canvasIds = await identityManager.GenerateUniqueIds<CanvasPainting>(customerId, count,
                cancellationToken);
            return new Stack<string>(canvasIds);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "Unable to generate {Count} unique CanvasPainting ids for customer {CustomerId}",
                count, customerId);
            return null;
        }
    }
    
    private (PresUpdateResult? updateResult, List<CanvasPainting>? canvasPaintings) GeneratePartialCanvasPaintingsFromAssets(
        int customerId, PresentationManifest presentationManifest)
    {
        var paintedResources = presentationManifest.PaintedResources!;
        
        var canvasPaintings = new List<CanvasPainting>();
        var count = 0;
        
        foreach (var paintedResource in paintedResources)
        {
            if (paintedResource.Asset == null) continue;
            
            
            var canvasOrder = paintedResource.CanvasPainting?.CanvasOrder ?? count;
            var (canvasIdErrors, specifiedCanvasId) =
                TryGetValidCanvasId(customerId, paintedResource, canvasPaintings, canvasOrder);
            if (canvasIdErrors != null) return (canvasIdErrors, null);
            
            var cp = new CanvasPainting
            {
                Label = paintedResource.CanvasPainting?.Label,
                CanvasLabel = paintedResource.CanvasPainting?.CanvasLabel,
                CustomerId = customerId,
                CanvasOrder = canvasOrder,
                AssetId = GetAssetIdForAsset(paintedResource.Asset, customerId),
                ChoiceOrder = paintedResource.CanvasPainting?.ChoiceOrder ?? -1,
                Ingesting = true,
                StaticWidth = paintedResource.CanvasPainting?.StaticWidth,
                StaticHeight = paintedResource.CanvasPainting?.StaticHeight,
                Duration = paintedResource.CanvasPainting?.Duration
            };
            
            if (specifiedCanvasId != null)
            {
                cp.Id = specifiedCanvasId;
            }
            
            count++;
            canvasPaintings.Add(cp);
        }
        
        return (null, canvasPaintings);
    }
    
    private async Task<(PresUpdateResult? updateResult, List<CanvasPainting>? canvasPaintings)> CreateCanvasPaintingsFromAssets(
        int customerId, PresentationManifest presentationManifest, CancellationToken cancellationToken = default)
    {
        var (updateResult, canvasPaintings) =
            GeneratePartialCanvasPaintingsFromAssets(customerId, presentationManifest);

        if (updateResult != null)
            return (updateResult, null);

        Debug.Assert(canvasPaintings is not null, "canvasPaintings is not null");
        
        var insertCanvasPaintingsError =
            await HandleInserts(canvasPaintings, customerId, cancellationToken);

        if (insertCanvasPaintingsError != null)
            return (insertCanvasPaintingsError, null);
        
        return (null, canvasPaintings);
    }

    private (PresUpdateResult? canvasIdErrors, string? specifiedCanvasId) TryGetValidCanvasId(
        int customerId,
        PaintedResource paintedResource,
        List<CanvasPainting> canvasPaintings,
        int canvasOrder)
    {
        try
        {
            paintedResource.CanvasPainting ??= new();
            var canvasId = GetCanvasId(customerId, paintedResource.CanvasPainting);

            return canvasId != null
                ? ValidateCanvasId(paintedResource.CanvasPainting, canvasPaintings, canvasOrder, canvasId)
                : (null, canvasId);
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Unable to parse canvas ID for {CustomerId}", customerId);
            return (ErrorHelper.InvalidCanvasId<PresentationManifest>(paintedResource.CanvasPainting.CanvasId), null);
        }
    }

    private static (PresUpdateResult? canvasIdErrors, string? specifiedCanvasId) ValidateCanvasId(
        Models.API.Manifest.CanvasPainting canvasPainting, List<CanvasPainting> canvasPaintings, int canvasOrder,
        string canvasId)
    {
        if (canvasPaintings.Where(c => c.CanvasOrder != canvasOrder).Any(c => c.Id == canvasId))
        {
            return (ErrorHelper.DuplicateCanvasId<PresentationManifest>(canvasId), null);
        }

        if (canvasPaintings.Where(c => c.CanvasOrder == canvasOrder).Any(c => c.Id != canvasId))
        {
            // this means that the canvas id has not been set, but there's a matching canvas painting record
            if (canvasPainting.CanvasId == null)
            {
                return (null, canvasPaintings.First(c => c.CanvasOrder == canvasOrder).Id);
            }

            return (ErrorHelper.CanvasOrderDifferentCanvasId<PresentationManifest>(canvasPainting.CanvasId), null);
        }

        return (null, canvasId);
    }

    // PK: Simplified by #340
    private static string? GetCanvasId(int customerId, Models.API.Manifest.CanvasPainting canvasPainting)
        => canvasPainting.CanvasId != null ? PathParser.GetCanvasId(canvasPainting, customerId) : null;

    private static AssetId GetAssetIdForAsset(JObject asset, int customerId)
    {
        // Read props from Asset - these must be there. If not, throw an exception
        var space = asset.GetRequiredValue(AssetProperties.Space);
        var id = asset.GetRequiredValue(AssetProperties.Id);
        return AssetId.FromString($"{customerId}/{space}/{id}");
    }
}
