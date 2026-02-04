using System.Data;
using System.Diagnostics;
using API.Features.Common.Helpers;
using API.Features.Manifest.Exceptions;
using API.Features.Storage.Helpers;
using API.Infrastructure.IdGenerator;
using Core.Exceptions;
using Core.Helpers;
using Models.API.Manifest;
using Models.Database;
using Services.Manifests;
using Services.Manifests.Exceptions;
using Services.Manifests.Helpers;
using Services.Manifests.Model;
using CanvasPainting = Models.Database.CanvasPainting;
using DbManifest = Models.Database.Collections.Manifest;
using PresUpdateResult = API.Infrastructure.Requests.ModifyEntityResult<Models.API.Manifest.PresentationManifest, Models.API.General.ModifyCollectionType>;

namespace API.Features.Manifest;

public class CanvasPaintingResolver(
    IdentityManager identityManager,
    ManifestItemsParser manifestItemsParser,
    ManifestPaintedResourceParser manifestPaintedResourceParser,
    ICanvasPaintingMerger canvasPaintingMerger,
    ILogger<CanvasPaintingResolver> logger)
{
    /// <summary>
    /// Generate new CanvasPainting objects for items in provided <see cref="PresentationManifest"/>
    /// </summary>
    /// <returns>Tuple of either error OR newly created </returns>
    public async Task<CanvasPaintingRecords> GenerateCanvasPaintings(
        int customerId, PresentationManifest presentationManifest, CancellationToken cancellationToken = default)
    {
        try
        {
            var manifestParseResult = ParseManifest(customerId, presentationManifest);
            if (manifestParseResult.Error != null) return CanvasPaintingRecords.Failure(manifestParseResult.Error);

            Debug.Assert(manifestParseResult.CanvasPaintings is not null, "manifestParseResult.CanvasPaintings is not null");

            var insertCanvasPaintingsError = await HandleInserts(manifestParseResult.CanvasPaintings, customerId, cancellationToken);
            if (insertCanvasPaintingsError != null) return CanvasPaintingRecords.Failure(insertCanvasPaintingsError);

            return CanvasPaintingRecords.Success(manifestParseResult.CanvasPaintings, manifestParseResult.AssetsIdentifiedInItems);
        }
        catch (InvalidCanvasIdException cpId)
        {
            logger.LogDebug(cpId, "InvalidCanvasId '{CanvasId}' encountered in {ManifestId}", cpId.CanvasId,
                presentationManifest.Id);
            return CanvasPaintingRecords.Failure(UpsertErrorHelper.InvalidCanvasId<PresentationManifest>(cpId.CanvasId, cpId.Message));
        }
        catch (PaintableAssetException paintableAssetException)
        {
            logger.LogError(paintableAssetException,
                "Error retrieving details of an asset from items when generating canvas paintings");
            return CanvasPaintingRecords.Failure(UpsertErrorHelper.PaintableAssetError<PresentationManifest>(paintableAssetException.Message));
        }
    }

    /// <summary>
    /// Generate and set <see cref="CanvasPainting"/> objects for items in provided <see cref="PresentationManifest"/>.
    /// Provided <see cref="PresentationManifest"/> is update to reflect required changes (ie canvasPaintings are
    /// created/updated/deleted accordingly) 
    /// </summary>
    /// <returns>Error, if processing fails</returns>
    public async Task<CanvasPaintingRecords> UpdateCanvasPaintings(int customerId, PresentationManifest presentationManifest,
        DbManifest existingManifest, CancellationToken cancellationToken = default)
    {
        var manifestParseResult = ParseManifest(customerId, presentationManifest);
        if (manifestParseResult.Error != null) return CanvasPaintingRecords.Failure(manifestParseResult.Error);
        
        existingManifest.CanvasPaintings ??= [];
        Debug.Assert(manifestParseResult.CanvasPaintings is not null, "manifestParseResult.CanvasPaintings is not null");
        
        var toInsert = UpdateCanvasPaintingRecords(existingManifest.CanvasPaintings, manifestParseResult.CanvasPaintings, presentationManifest.Space);
        
        var insertCanvasPaintingsError = await HandleInserts(toInsert, customerId, cancellationToken);
        if (insertCanvasPaintingsError != null) return CanvasPaintingRecords.Failure(insertCanvasPaintingsError);
        return CanvasPaintingRecords.Success(toInsert, manifestParseResult.AssetsIdentifiedInItems);
    }
    
    private List<InterimCanvasPainting> UpdateCanvasPaintingRecords(List<CanvasPainting> existingCanvasPaintings, 
        List<InterimCanvasPainting> incomingCanvasPaintings, string? existingSpace)
    {
        var processedCanvasPaintingIds = new List<int>(incomingCanvasPaintings.Count);
        var toInsert = new List<InterimCanvasPainting>();
        
        foreach (var incoming in incomingCanvasPaintings)
        {
            UpdateCanvasPainting(existingCanvasPaintings, incoming, processedCanvasPaintingIds, toInsert, existingSpace);
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
    
    private void UpdateCanvasPainting(List<CanvasPainting> existingCanvasPaintings, InterimCanvasPainting incoming,
        List<int> processedCanvasPaintingIds, List<InterimCanvasPainting> toInsert, string? existingSpace)
    {
        var candidates = GetCandidates(existingCanvasPaintings, incoming);
        var matching = TryFindMatching(incoming, processedCanvasPaintingIds, candidates);

        if (matching == null)
        {
            logger.LogTrace("Adding canvas {CanvasIndex}, choice {ChoiceIndex}", incoming.CanvasOrder,
                incoming.ChoiceOrder);
            
            // If there are other candidates with an id then assign that to incoming. This will be due to a matching
            // choice or composite canvas
            if (candidates.FirstOrDefault()?.Id is { Length: > 0 } existingId)
            {
                logger.LogTrace("Assigning id {CanvasId} to canvas {CanvasIndex}, choice {ChoiceIndex}", existingId,
                    incoming.CanvasOrder, incoming.ChoiceOrder);
                incoming.Id = existingId;
            }

            // Store it in a list for processing later (e.g. for bulk generation of UniqueIds)
            toInsert.Add(incoming);
        }
        else
        {
            int? space = null;
            if (existingSpace != null)
            {
                space = int.TryParse(existingSpace, out var convertedSpace) ? convertedSpace : null;
            }
            
            // Found matching DB record, update...
            logger.LogTrace("Updating canvasPaintingId {CanvasId}", matching.CanvasPaintingId);
            matching.UpdateFrom(incoming.ToCanvasPainting(space));
            processedCanvasPaintingIds.Add(matching.CanvasPaintingId);
        }
    }
    
    private static List<CanvasPainting> GetCandidates(List<CanvasPainting> existingCanvasPaintings, InterimCanvasPainting incoming)
    {
        if (incoming.Id is { Length: > 0 } incomingId)
        {
            // match by provided canvas id if provided
            return existingCanvasPaintings.Where(cp => cp.Id == incomingId).ToList();
        }

        // else match by original canvas id (if present, otherwise empty list)
        return existingCanvasPaintings.Where(cp =>
                incoming.CanvasOriginalId != null && cp.CanvasOriginalId == incoming.CanvasOriginalId)
            .ToList();
    }

    private CanvasPainting? TryFindMatching(InterimCanvasPainting incoming, List<int> processedCanvasPaintingIds,
        List<CanvasPainting> candidates)
    {
        var canvasLoggingId = GetCanvasLoggingId(incoming);
        CanvasPainting? matching = null;

        switch (candidates.Count)
        {
            case 1:
            {
                // Single item matching - check if we've processed it already.
                // If so this is due to choice OR multiple items on canvas
                var potential = candidates.Single();
                if (!processedCanvasPaintingIds.Contains(potential.CanvasPaintingId))
                {
                    logger.LogTrace("Found existing canvas painting for {CanvasLoggingId}", canvasLoggingId);
                    matching = potential;
                }

                break;
            }
            case > 1:
                // If there are multiple matching items then Canvas is a choice OR multi item canvas
                // If incoming has a choice, attempt to match existing candidate that has that choice order.
                // If incoming doesn't have a choice - then try to match on canvasOrder
                logger.LogTrace("Found multiple canvas paintings for {CanvasLoggingId}", canvasLoggingId);
                matching = incoming.ChoiceOrder.HasValue
                    ? candidates.FirstOrDefault(c => c.ChoiceOrder == incoming.ChoiceOrder)
                    : candidates.FirstOrDefault(c => c.CanvasOrder == incoming.CanvasOrder);
                break;
        }

        return matching;
    }

    private static string GetCanvasLoggingId(InterimCanvasPainting incoming)
    {
        return !string.IsNullOrEmpty(incoming.Id)
            ? incoming.Id
            : incoming.CanvasOriginalId?.ToString() ?? incoming.SuspectedAssetId ?? "unknown";
    }
    
    private async Task<PresUpdateResult?> HandleInserts(List<InterimCanvasPainting> canvasPaintings, int customerId,
        CancellationToken cancellationToken)
    {
        if (canvasPaintings.IsNullOrEmpty()) return null;

        logger.LogTrace("Adding {CanvasCounts} to Manifest", canvasPaintings.Count);
        var requiredIds = canvasPaintings.GetRequiredNumberOfCanvasIds();
        var canvasPaintingIds = await GenerateUniqueCanvasPaintingIds(requiredIds, customerId, cancellationToken);
        if (canvasPaintingIds == null) return UpsertErrorHelper.CannotGenerateUniqueId<PresentationManifest>();

        // Build a dictionary of canvas_grouping:canvas_id, this is populated as we iterate over canvas paintings.
        // We will also seed it with any 'new' items that are on the same canvas as these will have been prepopulated
        // with a canvas_id
        var canvasIds = canvasPaintings
            .Where(cp => !string.IsNullOrEmpty(cp.Id))
            .GroupBy(cp => cp.GetGroupingForIdAssignment()) 
            .ToDictionary(k => k.Key, v => v.First().Id); // the id will be the same in all items within a canvas
        foreach (var cp in canvasPaintings)
        {
            // CanvasPainting records that have the same CanvasOriginalId or CanvasOrder will share the same CanvasId
            var groupingValue = cp.GetGroupingForIdAssignment();
            if (canvasIds.TryGetValue(groupingValue, out var canvasOrderId))
            {
                cp.Id = canvasOrderId;
                continue;
            }

            // If item has an id, it's an update for a Choice so use the existing canvas_id. Else grab a new one
            var canvasId = string.IsNullOrEmpty(cp.Id) ? canvasPaintingIds.Pop() : cp.Id;
            canvasIds[groupingValue] = canvasId;
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
    
    private ManifestParseResult ParseManifest(int customerId, 
        PresentationManifest presentationManifest)
    {
        try
        {
            var paintedResourceCanvasPaintings = manifestPaintedResourceParser
                .ParseToCanvasPainting(presentationManifest, customerId).ToList();

            var itemsCanvasPaintings =
                manifestItemsParser
                    .ParseToCanvasPainting(presentationManifest, paintedResourceCanvasPaintings, customerId).ToList();

            var res = canvasPaintingMerger.CombinePaintedResources(itemsCanvasPaintings,
                paintedResourceCanvasPaintings, presentationManifest.Items);

            return new ManifestParseResult(null, res, itemsCanvasPaintings.GetItemsWithSuspectedAssets());
        }
        catch (InvalidCanvasIdException cpId)
        {
            logger.LogDebug(cpId, "InvalidCanvasId encountered in {ManifestId}", presentationManifest.Id);
            return new ManifestParseResult(UpsertErrorHelper.InvalidCanvasId<PresentationManifest>(cpId.CanvasId, cpId.Message), null, null);
        }
        catch (CanvasPaintingMergerException cpMergeError)
        {
            logger.LogDebug(cpMergeError,
                "Canvas painting merge exception encountered in {ManifestId} for id {Id} - expected: {Expected}, actual: {Actual}",
                presentationManifest.Id, cpMergeError.Id, cpMergeError.Expected, cpMergeError.Actual);
            return new ManifestParseResult(UpsertErrorHelper.ErrorMergingPaintedResourcesWithItems<PresentationManifest>(cpMergeError.Message),
                null, null);
        }
        catch (InvalidOperationException formatException)
        {
            logger.LogDebug(formatException,
                "Canvas painting exception encountered in {ManifestId}, could not retrieve an asset id",
                presentationManifest.Id);
            return new ManifestParseResult(UpsertErrorHelper.CouldNotRetrieveAssetId<PresentationManifest>(), null, null);
        }
        catch (PaintableAssetException paintableAssetException)
        {
            logger.LogDebug(paintableAssetException,
                "Error retrieving paintable assets from items");
            return new ManifestParseResult(UpsertErrorHelper.PaintableAssetError<PresentationManifest>(paintableAssetException.Message), null, null);
        }
    }

    private class ManifestParseResult(
        PresUpdateResult? error,
        List<InterimCanvasPainting>? canvasPaintings,
        List<InterimCanvasPainting>? assetsIdentifiedInItems)
    {
        public PresUpdateResult? Error { get; set; } = error;

        public List<InterimCanvasPainting>? CanvasPaintings { get; set; } = canvasPaintings;

        public List<InterimCanvasPainting>? AssetsIdentifiedInItems { get; set; } = assetsIdentifiedInItems;
    }
}
