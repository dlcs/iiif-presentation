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
using Microsoft.EntityFrameworkCore;
using Models.API.Manifest;
using Models.Database;
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
        // TODO - do I need this??
        if (canvasPaintings.Any())
        {
            // TODO - make this logic shared for Create and Update
            var requiredIds = canvasPaintings.DistinctBy(c => c.CanvasOrder).Count();
            var canvasPaintingIds = await GenerateUniqueCanvasPaintingIds(requiredIds, request, cancellationToken);
            if (canvasPaintingIds.IsNullOrEmpty())
                return (ErrorHelper.CannotGenerateUniqueId<PresentationManifest>(), null);

            var canvasIds = new Dictionary<int, string>(requiredIds);
            int count = 0;
            foreach (var cp in canvasPaintings)
            {
                // CanvasPainting records that have the same CanvasOrder will share the same CanvasId 
                if (canvasIds.TryGetValue(cp.CanvasOrder, out var canvasOrderId))
                {
                    cp.Id = canvasOrderId;
                    continue;
                }

                var canvasId = canvasPaintingIds[count++];
                canvasIds[cp.CanvasOrder] = canvasId;
                cp.Id = canvasId;
            }
        }

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
        
        existingManifest.CanvasPaintings ??= new List<CanvasPainting>();
        
        // Iterate through all incoming - this is what we want to preserve in DB
        var processedCanvasPaintingIds = new List<int>(incomingCanvasPaintings.Count);
        var toInsert = new List<CanvasPainting>();
        foreach (var incoming in incomingCanvasPaintings)
        {
            var matching =
                existingManifest.CanvasPaintings.SingleOrDefault(cp =>
                    cp.CanvasOriginalId == incoming.CanvasOriginalId);

            // If it's not in DB, create...
            if (matching == null)
            {
                // Store it in a list for processing later (e.g. for bulk generation of UniqueIds)
                toInsert.Add(incoming);
            }
            else
            {
                // Found matching DB record, update..
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

        if (toInsert.Count > 0)
        {
            // TODO - make this logic shared for Create and Update
            logger.LogTrace("Adding {CanvasCounts} to Manifest", toInsert.Count);
            var requiredIds = toInsert.DistinctBy(c => c.CanvasOrder).Count();
            var canvasPaintingIds = await GenerateUniqueCanvasPaintingIds(requiredIds, request, cancellationToken);
            if (canvasPaintingIds.IsNullOrEmpty())
                return (ErrorHelper.CannotGenerateUniqueId<PresentationManifest>(), null);

            var canvasIds = new Dictionary<int, string>(requiredIds);
            int count = 0;
            foreach (var cp in toInsert)
            {
                // CanvasPainting records that have the same CanvasOrder will share the same CanvasId 
                if (canvasIds.TryGetValue(cp.CanvasOrder, out var canvasOrderId))
                {
                    cp.Id = canvasOrderId;
                    continue;
                }

                var canvasId = canvasPaintingIds[count++];
                canvasIds[cp.CanvasOrder] = canvasId;
                cp.Id = canvasId;
            }
            existingManifest.CanvasPaintings.AddRange(toInsert);
        }
        
        existingManifest.Modified = DateTime.UtcNow;
        existingManifest.ModifiedBy = Authorizer.GetUser();
        existingManifest.Label = request.PresentationManifest.Label;
        var canonicalHierarchy = existingManifest.Hierarchy!.Single(c => c.Canonical);
        canonicalHierarchy.Slug = request.PresentationManifest.Slug!;
        canonicalHierarchy.Parent = parentCollection.Id;

        var saveErrors = await SaveAndPopulateEntity(request, existingManifest, cancellationToken);
        return (saveErrors, existingManifest);
    }

    private async Task<PresUpdateResult?> SaveAndPopulateEntity(WriteManifestRequest request, DbManifest dbManifest, CancellationToken cancellationToken)
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
            // TODO - a bulk version of this for multiple CanvasPaintings?
            // TODO this can't be done for CanvasId. CanvasId needs to be unique but there can be multiple rows
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
            // TODO - a bulk version of this for multiple CanvasPaintings?
            // TODO this can't be done for CanvasId. CanvasId needs to be unique but there can be multiple rows
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
