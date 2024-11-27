using System.Data;
using API.Auth;
using API.Converters;
using API.Features.Manifest.Helpers;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.AWS;
using API.Infrastructure.Helpers;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Validation;
using Core;
using Core.Helpers;
using IIIF.Serialisation;
using Models.API.Manifest;
using Models.Database.General;
using Repository;
using Repository.Manifests;
using Repository.Helpers;
using CanvasPainting = Models.Database.CanvasPainting;
using Collection = Models.Database.Collections.Collection;
using DbManifest = Models.Database.Collections.Manifest;
using PresUpdateResult = API.Infrastructure.Requests.ModifyEntityResult<Models.API.Manifest.PresentationManifest, Models.API.General.ModifyCollectionType>;

namespace API.Features.Manifest;

/// <summary>
/// Record containing fields for Upserting a Manifest
/// </summary>
public record UpsertManifestRequest(
    string ManifestId,
    string? Etag,
    int CustomerId,
    PresentationManifest PresentationManifest,
    string RawRequestBody,
    UrlRoots UrlRoots) : WriteManifestRequest(CustomerId, PresentationManifest, RawRequestBody, UrlRoots);

/// <summary>
/// Record containing fields for creating a Manifest
/// </summary>
public record WriteManifestRequest(
    int CustomerId,
    PresentationManifest PresentationManifest,
    string RawRequestBody,
    UrlRoots UrlRoots);

/// <summary>
/// Service to help with creation of manifests
/// </summary>
public class ManifestService(
    PresentationContext dbContext,
    IdentityManager identityManager,
    IIIFS3Service iiifS3,
    IETagManager eTagManager,
    ManifestItemsParser manifestItemsParser,
    ILogger<ManifestService> logger)
{
    /// <summary>
    /// Create or update full manifest, using details provided in request object
    /// </summary>
    public async Task<PresUpdateResult> Upsert(UpsertManifestRequest request, CancellationToken cancellationToken)
    {
        var existingManifest =
            await dbContext.RetrieveManifestAsync(request.CustomerId, request.ManifestId, true, cancellationToken);

        if (existingManifest == null)
        {
            if (!string.IsNullOrEmpty(request.Etag)) return ErrorHelper.EtagNotRequired<PresentationManifest>();
            
            logger.LogDebug("Manifest {ManifestId} for Customer {CustomerId} doesn't exist, creating",
                request.ManifestId, request.CustomerId);
            return await CreateInternal(request, request.ManifestId, cancellationToken);
        }
        
        return await UpdateInternal(request, existingManifest, cancellationToken);
    }

    /// <summary>
    /// Create new manifest, using details provided in request object
    /// </summary>
    public Task<PresUpdateResult> Create(WriteManifestRequest request, CancellationToken cancellationToken)
        => CreateInternal(request, null, cancellationToken);
    
    private async Task<PresUpdateResult> CreateInternal(WriteManifestRequest request, string? manifestId, CancellationToken cancellationToken)
    {
        var (parentErrors, parentCollection) = await TryGetParent(request, cancellationToken);
        if (parentErrors != null) return parentErrors;
        
        var (error, dbManifest) = await CreateDatabaseRecord(request, parentCollection!, manifestId, cancellationToken);
        if (error != null) return error; 

        await SaveToS3(dbManifest!, request, cancellationToken);
        
        return PresUpdateResult.Success(
            request.PresentationManifest.SetGeneratedFields(dbManifest!, request.UrlRoots), WriteResult.Created);
    }
    
    private async Task<PresUpdateResult> UpdateInternal(UpsertManifestRequest request,
        DbManifest existingManifest, CancellationToken cancellationToken)
    {
        if (!eTagManager.TryGetETag(existingManifest, out var eTag) || eTag != request.Etag)
        {
            return ErrorHelper.EtagNonMatching<PresentationManifest>();
        }
        
        var (parentErrors, parentCollection) = await TryGetParent(request, cancellationToken);
        if (parentErrors != null) return parentErrors;

        using (logger.BeginScope("Manifest {ManifestId} for Customer {CustomerId} exists, updating",
                   request.ManifestId, request.CustomerId))
        {
            var (error, dbManifest) =
                await UpdateDatabaseRecord(request, parentCollection!, existingManifest, cancellationToken);
            if (error != null) return error;

            await SaveToS3(dbManifest!, request, cancellationToken);

            return PresUpdateResult.Success(
                request.PresentationManifest.SetGeneratedFields(dbManifest!, request.UrlRoots), WriteResult.Updated);
        }
    }

    private async Task<(PresUpdateResult? parentErrors, Collection? parentCollection)> TryGetParent(
        WriteManifestRequest request, CancellationToken cancellationToken)
    {
        var manifest = request.PresentationManifest;
        var urlRoots = request.UrlRoots;;
        var parentCollection = await dbContext.RetrieveCollectionAsync(request.CustomerId,
            manifest.GetParentSlug(), cancellationToken: cancellationToken);
        
        // Validation
        if (parentCollection == null) return (ErrorHelper.NullParentResponse<PresentationManifest>(), null);
        if (!parentCollection.IsStorageCollection) return (ManifestErrorHelper.ParentMustBeStorageCollection<PresentationManifest>(), null);
        if (manifest.IsUriParentInvalid(parentCollection, urlRoots)) return (ErrorHelper.NullParentResponse<PresentationManifest>(), null);

        return (null, parentCollection);
    }

    private async Task<(PresUpdateResult?, DbManifest?)> CreateDatabaseRecord(WriteManifestRequest request,
        Collection parentCollection, string? requestedId, CancellationToken cancellationToken)
    {
        var id = requestedId ?? await GenerateUniqueManifestId(request, cancellationToken);
        if (id == null) return (ErrorHelper.CannotGenerateUniqueId<PresentationManifest>(), null);
        
        var canvasPaintings = manifestItemsParser.ParseItemsToCanvasPainting(request.PresentationManifest).ToList();
        var insertCanvasPaintingsError = await HandleInserts(canvasPaintings, request, cancellationToken);
        if (insertCanvasPaintingsError != null) return (insertCanvasPaintingsError, null);
        
        var timeStamp = DateTime.UtcNow;
        var dbManifest = new DbManifest
        {
            Id = id,
            CustomerId = request.CustomerId,
            Created = timeStamp,
            Modified = timeStamp,
            CreatedBy = Authorizer.GetUser(),
            Label = request.PresentationManifest.Label,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = request.PresentationManifest.Slug!,
                    Canonical = true,
                    Type = ResourceType.IIIFManifest,
                    Parent = parentCollection.Id,
                }
            ],
            CanvasPaintings = canvasPaintings,
        };
        
        await dbContext.AddAsync(dbManifest, cancellationToken);

        var saveErrors = await SaveAndPopulateEntity(request, dbManifest, cancellationToken);
        return (saveErrors, dbManifest);
    }

    private async Task<(PresUpdateResult?, DbManifest?)> UpdateDatabaseRecord(WriteManifestRequest request,
        Collection parentCollection, DbManifest existingManifest, CancellationToken cancellationToken)
    {
        var incomingCanvasPaintings =
            manifestItemsParser.ParseItemsToCanvasPainting(request.PresentationManifest).ToList();
        
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
                    matching = candidates.Single();
                }
            }
            else if (candidates.Count > 1)
            {
                // If there are multiple matching items then Canvas is a choice
                matching = candidates.SingleOrDefault(c => c.ChoiceOrder == incoming.ChoiceOrder);
            }
            
            if (matching == null)
            {
                // If it's not in DB, create...
                if (incoming.ChoiceOrder.HasValue)
                {
                    // This is a choice. If there are other, existing items for the same canvas then seed canvas_id
                    incoming.Id = candidates.SingleOrDefault()?.Id;
                }
                
                // Store it in a list for processing later (e.g. for bulk generation of UniqueIds)
                toInsert.Add(incoming);
            }
            else
            {
                // Found matching DB record, update...
                logger.LogTrace("Updating canvasPaintingId {CanvasId}", matching.CanvasPaintingId);
                matching.Label = incoming.Label;
                matching.CanvasLabel = incoming.CanvasLabel;
                matching.CanvasOrder = incoming.CanvasOrder;
                matching.ChoiceOrder = incoming.ChoiceOrder;
                matching.Thumbnail = incoming.Thumbnail;
                matching.StaticHeight = incoming.StaticHeight;
                matching.StaticWidth = incoming.StaticWidth;
                matching.Target = incoming.Target;
                matching.Modified = DateTime.UtcNow;
                processedCanvasPaintingIds.Add(matching.CanvasPaintingId);
            }
        }

        foreach (var toRemove in existingManifest.CanvasPaintings
                     .Where(cp => !processedCanvasPaintingIds.Contains(cp.CanvasPaintingId)).ToList())
        {
            logger.LogTrace("Deleting canvasPaintingId {CanvasId}", toRemove.CanvasPaintingId);
            existingManifest.CanvasPaintings.Remove(toRemove);
        }
        
        var insertCanvasPaintingsError = await HandleInserts(toInsert, request, cancellationToken);
        if (insertCanvasPaintingsError != null) return (insertCanvasPaintingsError, null);
        existingManifest.CanvasPaintings.AddRange(toInsert);
        existingManifest.Modified = DateTime.UtcNow;
        existingManifest.ModifiedBy = Authorizer.GetUser();
        existingManifest.Label = request.PresentationManifest.Label;
        var canonicalHierarchy = existingManifest.Hierarchy!.Single(c => c.Canonical);
        canonicalHierarchy.Slug = request.PresentationManifest.Slug!;
        canonicalHierarchy.Parent = parentCollection.Id;

        var saveErrors = await SaveAndPopulateEntity(request, existingManifest, cancellationToken);
        return (saveErrors, existingManifest);
    }
    
    private async Task<PresUpdateResult?> HandleInserts(List<CanvasPainting> canvasPaintings, WriteManifestRequest request, CancellationToken cancellationToken)
    {
        if (canvasPaintings.IsNullOrEmpty()) return null;
        
        logger.LogTrace("Adding {CanvasCounts} to Manifest", canvasPaintings.Count);
        var requiredIds = canvasPaintings
            .Where(cp => string.IsNullOrEmpty(cp.Id))
            .DistinctBy(c => c.CanvasOrder)
            .Count();
        var canvasPaintingIds = await GenerateUniqueCanvasPaintingIds(requiredIds, request, cancellationToken);
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

    private async Task<PresUpdateResult?> SaveAndPopulateEntity(WriteManifestRequest request, DbManifest dbManifest,
        CancellationToken cancellationToken)
    {
        var saveErrors =
            await dbContext.TrySave<PresentationManifest>("manifest", request.CustomerId, logger, cancellationToken);

        if (saveErrors != null) return saveErrors;

        dbManifest.Hierarchy.Single().FullPath =
            await ManifestRetrieval.RetrieveFullPathForManifest(dbManifest.Id, dbManifest.CustomerId, dbContext,
                cancellationToken);
        return null;
    }

    private async Task<string?> GenerateUniqueManifestId(WriteManifestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await identityManager.GenerateUniqueId<DbManifest>(request.CustomerId, cancellationToken);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "Unable to generate a unique manifest id for customer {CustomerId}",
                request.CustomerId);
            return null;
        }
    }
    
    private async Task<IList<string>?> GenerateUniqueCanvasPaintingIds(int count, WriteManifestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (count == 0) return [];
            
            return await identityManager.GenerateUniqueIds<CanvasPainting>(request.CustomerId, count,
                cancellationToken);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "Unable to generate unique CanvasPainting ids for customer {CustomerId}",
                request.CustomerId);
            return null;
        }
    }

    private async Task SaveToS3(DbManifest dbManifest, WriteManifestRequest request, CancellationToken cancellationToken)
    {
        var iiifManifest = request.RawRequestBody.FromJson<IIIF.Presentation.V3.Manifest>();
        await iiifS3.SaveIIIFToS3(iiifManifest, dbManifest, dbManifest.GenerateFlatManifestId(request.UrlRoots),
            cancellationToken);
    }
}
