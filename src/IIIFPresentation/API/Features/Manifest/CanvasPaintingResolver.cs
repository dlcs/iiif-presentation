using System.Data;
using System.Diagnostics;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.IdGenerator;
using Core.Exceptions;
using Core.Helpers;
using Models.API.Manifest;
using Models.Database;
using Repository.Manifests;
using CanvasPainting = Models.Database.CanvasPainting;
using DbManifest = Models.Database.Collections.Manifest;
using PresUpdateResult = API.Infrastructure.Requests.ModifyEntityResult<Models.API.Manifest.PresentationManifest, Models.API.General.ModifyCollectionType>;

namespace API.Features.Manifest;

public class CanvasPaintingResolver(
    IdentityManager identityManager,
    ManifestItemsParser manifestItemsParser,
    ManifestPaintedResourceParser manifestPaintedResourceParser,
    ILogger<CanvasPaintingResolver> logger)
{
    /// <summary>
    /// Generate new CanvasPainting objects for items in provided <see cref="PresentationManifest"/>
    /// </summary>
    /// <returns>Tuple of either error OR newly created </returns>
    public async Task<(PresUpdateResult? updateResult, List<CanvasPainting>? canvasPaintings)> GenerateCanvasPaintings(
        int customerId, PresentationManifest presentationManifest, CancellationToken cancellationToken = default)
    {
        var parser = GetParserForManifest(presentationManifest);
        return await HandleInsert(parser, customerId, presentationManifest, cancellationToken);
    }

    /// <summary>
    /// Generate and set <see cref="CanvasPainting"/> objects for items in provided <see cref="PresentationManifest"/>.
    /// Provided <see cref="PresentationManifest"/> is update to reflect required changes (ie canvasPaintings are
    /// created/updated/deleted accordingly) 
    /// </summary>
    /// <returns>Error, if processing fails</returns>
    public async Task<PresUpdateResult?> UpdateCanvasPaintings(int customerId, PresentationManifest presentationManifest,
        DbManifest existingManifest, CancellationToken cancellationToken = default)
    {
        var parser = GetParserForManifest(presentationManifest);
        return await HandleUpdate(parser, customerId, presentationManifest, existingManifest, cancellationToken);
    }
    
    private ICanvasPaintingParser GetParserForManifest(PresentationManifest presentationManifest)
    {
        ICanvasPaintingParser parser = presentationManifest.PaintedResources.HasAsset()
            ? manifestPaintedResourceParser
            : manifestItemsParser;
        return parser;
    }

    private async Task<PresUpdateResult?> HandleUpdate(ICanvasPaintingParser parser, int customerId,
        PresentationManifest presentationManifest, DbManifest existingManifest, CancellationToken cancellationToken)
    {
        var (error, incomingCanvasPaintings) = ParseManifest(parser, customerId, presentationManifest);
        if (error != null) return error;
        
        existingManifest.CanvasPaintings ??= [];
        Debug.Assert(incomingCanvasPaintings is not null, "incomingCanvasPaintings is not null");
        
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
            // Found matching DB record, update...
            logger.LogTrace("Updating canvasPaintingId {CanvasId}", matching.CanvasPaintingId);
            matching.UpdateFrom(incoming);
            processedCanvasPaintingIds.Add(matching.CanvasPaintingId);
        }
    }
    
    private static List<CanvasPainting> GetCandidates(List<CanvasPainting> existingCanvasPaintings, CanvasPainting incoming)
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

    private CanvasPainting? TryFindMatching(CanvasPainting incoming, List<int> processedCanvasPaintingIds,
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
                // TODO is it safe to assume we have a usable CanvasOrder? do we need to defend against not having it, or it coming from "items" automatically?
                logger.LogTrace("Found multiple canvas paintings for {CanvasLoggingId}", canvasLoggingId);
                matching = incoming.ChoiceOrder.HasValue
                    ? candidates.FirstOrDefault(c => c.ChoiceOrder == incoming.ChoiceOrder)
                    : candidates.FirstOrDefault(c => c.CanvasOrder == incoming.CanvasOrder);
                break;
        }

        return matching;
    }

    private static string GetCanvasLoggingId(CanvasPainting incoming)
    {
        return !string.IsNullOrEmpty(incoming.Id)
            ? incoming.Id
            : incoming.CanvasOriginalId?.ToString() ?? incoming.AssetId?.ToString() ?? "unknown";
    }

    private async Task<PresUpdateResult?> HandleInserts(List<CanvasPainting> canvasPaintings, int customerId,
        CancellationToken cancellationToken)
    {
        if (canvasPaintings.IsNullOrEmpty()) return null;

        logger.LogTrace("Adding {CanvasCounts} to Manifest", canvasPaintings.Count);
        var requiredIds = canvasPaintings.GetRequiredNumberOfCanvasIds();
        var canvasPaintingIds = await GenerateUniqueCanvasPaintingIds(requiredIds, customerId, cancellationToken);
        if (canvasPaintingIds == null) return ErrorHelper.CannotGenerateUniqueId<PresentationManifest>();

        // Build a dictionary of canvas_grouping:canvas_id, this is populated as we iterate over canvas paintings.
        // We will also seed it with any 'new' items that are actually new Choices as these will have been prepopulated
        // with a canvas_id
        var canvasIds = canvasPaintings
            .Where(cp => !string.IsNullOrEmpty(cp.Id))
            .GroupBy(cp => cp.GetGroupingForIdAssignment()) // grouping by canvas order avoids issues with choices providing duplicate canvas ids
            .ToDictionary(k => k.Key, v => v.First().Id); // the id will be the same in all items within a choice construct
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

    private async Task<(PresUpdateResult? error, List<CanvasPainting>? canvasPaintings)> HandleInsert(
        ICanvasPaintingParser parser, int customerId, PresentationManifest presentationManifest,
        CancellationToken cancellationToken)
    {
        try
        {
            var (parseError, canvasPaintings) = ParseManifest(parser, customerId, presentationManifest);
            if (parseError != null) return (parseError, null);

            Debug.Assert(canvasPaintings is not null, "canvasPaintings is not null");

            var insertCanvasPaintingsError = await HandleInserts(canvasPaintings, customerId, cancellationToken);
            if (insertCanvasPaintingsError != null) return (insertCanvasPaintingsError, null);

            return (null, canvasPaintings);
        }
        catch (InvalidCanvasIdException cpId)
        {
            logger.LogDebug(cpId, "InvalidCanvasId '{CanvasId}' encountered in {ManifestId}", cpId.CanvasId,
                presentationManifest.Id);
            return (ErrorHelper.InvalidCanvasId<PresentationManifest>(cpId.CanvasId), null);
        }
    }
    
    private (PresUpdateResult? error, List<CanvasPainting>? canvasPaintings) ParseManifest(
        ICanvasPaintingParser parser, int customerId, PresentationManifest presentationManifest)
    {
        try
        {
            var res = parser.ParseToCanvasPainting(presentationManifest, customerId).ToList();
            return (null, res);
        }
        catch (InvalidCanvasIdException cpId)
        {
            logger.LogDebug(cpId, "InvalidCanvasId encountered in {ManifestId}", presentationManifest.Id);
            return (ErrorHelper.InvalidCanvasId<PresentationManifest>(cpId.CanvasId), null);
        }
    }
}
