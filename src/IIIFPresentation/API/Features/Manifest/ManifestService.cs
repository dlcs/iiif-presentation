using System.Data;
using API.Converters;
using API.Features.Manifest.Helpers;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.AWS;
using API.Infrastructure.Helpers;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Validation;
using Core;
using Core.Auth;
using Core.Helpers;
using DLCS;
using DLCS.API;
using DLCS.Exceptions;
using IIIF.Serialisation;
using Microsoft.Extensions.Options;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.Collections;
using Models.Database.General;
using Newtonsoft.Json.Linq;
using Repository;
using Repository.Helpers;
using Batch = DLCS.Models.Batch;
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
    bool CreateSpace) : WriteManifestRequest(CustomerId, PresentationManifest, RawRequestBody, CreateSpace);

/// <summary>
/// Record containing fields for creating a Manifest
/// </summary>
public record WriteManifestRequest(
    int CustomerId,
    PresentationManifest PresentationManifest,
    string RawRequestBody,
    bool CreateSpace);

/// <summary>
/// Service to help with creation of manifests
/// </summary>
public class ManifestService(
    PresentationContext dbContext,
    IdentityManager identityManager,
    IIIFS3Service iiifS3,
    IETagManager eTagManager,
    CanvasPaintingResolver canvasPaintingResolver,
    IPathGenerator pathGenerator,
    IDlcsApiClient dlcsApiClient,
    ILogger<ManifestService> logger)
{
    /// <summary>
    /// Create or update full manifest, using details provided in request object
    /// </summary>
    public async Task<PresUpdateResult> Upsert(UpsertManifestRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var existingManifest =
                await dbContext.RetrieveManifestAsync(request.CustomerId, request.ManifestId, true, true, cancellationToken);

            if (existingManifest == null)
            {
                if (!string.IsNullOrEmpty(request.Etag)) return ErrorHelper.EtagNotRequired<PresentationManifest>();

                logger.LogDebug("Manifest {ManifestId} for Customer {CustomerId} doesn't exist, creating",
                    request.ManifestId, request.CustomerId);
                return await CreateInternal(request, request.ManifestId, cancellationToken);
            }

            return await UpdateInternal(request, existingManifest, cancellationToken);
        }
        catch (DlcsException)
        {
            return ErrorHelper.ErrorCreatingSpace<PresentationManifest>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error upserting manifest {ManifestId} for customer {CustomerId}", request.ManifestId,
                request.CustomerId);
            return PresUpdateResult.Failure($"Unexpected error upserting manifest {request.ManifestId}",
                ModifyCollectionType.Unknown, WriteResult.Error);
        }
    }

    /// <summary>
    /// Create new manifest, using details provided in request object
    /// </summary>
    public async Task<PresUpdateResult> Create(WriteManifestRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await CreateInternal(request, null, cancellationToken);
        }
        catch (DlcsException)
        {
            return ErrorHelper.ErrorCreatingSpace<PresentationManifest>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating manifest for customer {CustomerId}", request.CustomerId);
            return PresUpdateResult.Failure("Unexpected error creating manifest", ModifyCollectionType.Unknown,
                WriteResult.Error);
        }
    }

    private async Task<PresUpdateResult> CreateInternal(WriteManifestRequest request, string? manifestId, CancellationToken cancellationToken)
    {
        var (parentErrors, parentCollection) = await TryGetParent(request, cancellationToken);
        if (parentErrors != null) return parentErrors;

        using (logger.BeginScope("Creating Manifest for Customer {CustomerId}", request.CustomerId))
        {
            var (error, dbManifest) =
                await CreateDatabaseRecordAndIiifCloudServicesInteractions(request, parentCollection!, manifestId, cancellationToken);
            if (error != null) return error;

            await SaveToS3(dbManifest!, request, cancellationToken);
            return PresUpdateResult.Success(
                request.PresentationManifest.SetGeneratedFields(dbManifest!, pathGenerator), WriteResult.Created);
        }
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

        using (logger.BeginScope("Updating Manifest {ManifestId} for Customer {CustomerId}",
                   request.ManifestId, request.CustomerId))
        {
            var (error, dbManifest) =
                await UpdateDatabaseRecord(request, parentCollection!, existingManifest, cancellationToken);
            if (error != null) return error;

            await SaveToS3(dbManifest!, request, cancellationToken);

            return PresUpdateResult.Success(
                request.PresentationManifest.SetGeneratedFields(dbManifest!, pathGenerator), WriteResult.Updated);
        }
    }

    private bool CheckForItemsAndPaintedResources(PresentationManifest presentationManifest)
    {
        return !presentationManifest.Items.IsNullOrEmpty() &&
               !presentationManifest.PaintedResources.IsNullOrEmpty();
    }

    private async Task<(PresUpdateResult? parentErrors, Collection? parentCollection)> TryGetParent(
        WriteManifestRequest request, CancellationToken cancellationToken)
    {
        var manifest = request.PresentationManifest;
        var parentCollection = await dbContext.RetrieveCollectionAsync(request.CustomerId,
            manifest.GetParentSlug(), cancellationToken: cancellationToken);
        
        // Validation
        if (parentCollection == null) return (ErrorHelper.NullParentResponse<PresentationManifest>(), null);
        if (!parentCollection.IsStorageCollection) return (ManifestErrorHelper.ParentMustBeStorageCollection<PresentationManifest>(), null);
        if (manifest.IsUriParentInvalid(parentCollection, pathGenerator)) return (ErrorHelper.NullParentResponse<PresentationManifest>(), null);

        return (null, parentCollection);
    }

    private async Task<(PresUpdateResult?, DbManifest?)> CreateDatabaseRecordAndIiifCloudServicesInteractions(WriteManifestRequest request,
        Collection parentCollection, string? requestedId, CancellationToken cancellationToken)
    {
        if (!request.CreateSpace && request.PresentationManifest.PaintedResources.HasAsset())
        {
            return (ErrorHelper.SpaceRequired<PresentationManifest>(), null);
        }
        
        var manifestId = requestedId ?? await GenerateUniqueManifestId(request, cancellationToken);
        if (manifestId == null) return (ErrorHelper.CannotGenerateUniqueId<PresentationManifest>(), null);
        
        var spaceIdTask = CreateSpaceIfRequired(request.CustomerId, manifestId, request.CreateSpace, cancellationToken);
        
        PresUpdateResult? canvasPaintingsError;
        List<CanvasPainting>? canvasPaintings;

        if (request.PresentationManifest.PaintedResources.HasAsset())
        {
            (canvasPaintingsError, canvasPaintings) = await canvasPaintingResolver.CreateCanvasPaintingsFromAssets(request.CustomerId,
                request.PresentationManifest,
                (await spaceIdTask)!.Value, cancellationToken);
        }
        else
        {
            (canvasPaintingsError, canvasPaintings) =
                await canvasPaintingResolver.InsertCanvasPaintingsFromItems(request.CustomerId,
                    request.PresentationManifest, cancellationToken);
        }
        
        if (canvasPaintingsError != null) return (canvasPaintingsError, null);

        var timeStamp = DateTime.UtcNow;
        var dbManifest = new DbManifest
        {
            Id = manifestId,
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
            SpaceId = await spaceIdTask
        };
        
        await dbContext.AddAsync(dbManifest, cancellationToken);
        
        if (request.PresentationManifest.PaintedResources.HasAsset())
        {
            try
            {
                var batches = await dlcsApiClient.IngestAssets(request.CustomerId,
                    request.PresentationManifest.PaintedResources!.Select(p => p.Asset).ToList()!,
                    cancellationToken);
                    
                await batches.SaveBatchesInDlcs(dbManifest, dbContext, cancellationToken);
            }
            catch (DlcsException exception)
            {
                logger.LogError(exception, "Error creating batch request for customer {CustomerId}", request.CustomerId);
                return (PresUpdateResult.Failure("Failed to upload assets into the DLCS", ModifyCollectionType.Unknown,
                    WriteResult.Error), null);
            }
        }

        var saveErrors = await SaveAndPopulateEntity(request, dbManifest, cancellationToken);
        return (saveErrors, dbManifest);
    }
    
    private async Task<int?> CreateSpaceIfRequired(int customerId, string manifestId, bool createSpace,
        CancellationToken cancellationToken)
    {
        if (!createSpace) return null;
        
        logger.LogDebug("Creating new space for customer {Customer}", customerId);
        var newSpace =
            await dlcsApiClient.CreateSpace(customerId, ManifestX.GetDefaultSpaceName(manifestId), cancellationToken);
        return newSpace.Id;
    }

    private async Task<(PresUpdateResult?, DbManifest?)> UpdateDatabaseRecord(WriteManifestRequest request,
        Collection parentCollection, DbManifest existingManifest, CancellationToken cancellationToken)
    {
        var canvasPaintingsError = await canvasPaintingResolver.UpdateCanvasPaintings(request.CustomerId,
            request.PresentationManifest, existingManifest, cancellationToken);
        if (canvasPaintingsError != null) return (canvasPaintingsError, null);
        
        existingManifest.Modified = DateTime.UtcNow;
        existingManifest.ModifiedBy = Authorizer.GetUser();
        existingManifest.Label = request.PresentationManifest.Label;
        var canonicalHierarchy = existingManifest.Hierarchy!.Single(c => c.Canonical);
        canonicalHierarchy.Slug = request.PresentationManifest.Slug!;
        canonicalHierarchy.Parent = parentCollection.Id;

        var saveErrors = await SaveAndPopulateEntity(request, existingManifest, cancellationToken);
        return (saveErrors, existingManifest);
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

    private async Task SaveToS3(DbManifest dbManifest, WriteManifestRequest request, CancellationToken cancellationToken)
    {
        var iiifManifest = request.RawRequestBody.FromJson<IIIF.Presentation.V3.Manifest>();
        await iiifS3.SaveIIIFToS3(iiifManifest, dbManifest, pathGenerator.GenerateFlatManifestId(dbManifest),
            cancellationToken);
    }
}
