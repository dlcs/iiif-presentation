using System.Data;
using System.Diagnostics;
using API.Converters;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Helpers;
using API.Infrastructure.IdGenerator;
using AWS.Helpers;
using Core;
using Core.Auth;
using Core.IIIF;
using DLCS.Exceptions;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Models.API.General;
using Models.API.Manifest;
using Models.Database;
using Models.Database.General;
using Models.DLCS;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using Services.Manifests.AWS;
using Services.Manifests.Helpers;
using DbManifest = Models.Database.Collections.Manifest;
using PresUpdateResult = API.Infrastructure.Requests.ModifyEntityResult<Models.API.Manifest.PresentationManifest, Models.API.General.ModifyCollectionType>;

namespace API.Features.Manifest;

/// <summary>
/// Record containing fields for Upserting a Manifest
/// </summary>
public class UpsertManifestRequest(
    string manifestId,
    string? etag,
    int customerId,
    PresentationManifest presentationManifest,
    string rawRequestBody,
    bool createSpace) : WriteManifestRequest(customerId, presentationManifest, rawRequestBody, createSpace)
{
    public string ManifestId { get; } = manifestId;
    public string? Etag { get; } = etag;
}

/// <summary>
/// Record containing fields for creating a Manifest
/// </summary>
public class WriteManifestRequest
{
    public WriteManifestRequest(int customerId,
        PresentationManifest presentationManifest,
        string rawRequestBody,
        bool createSpace)
    {
        // removes presentation behaviors that aren't required for a manifest
        presentationManifest.RemovePresentationBehaviours();
        
        CustomerId = customerId;
        PresentationManifest = presentationManifest;
        RawRequestBody = rawRequestBody;
        CreateSpace = createSpace;
    }
    
    public int CustomerId { get; }
    public PresentationManifest PresentationManifest { get; }
    public string RawRequestBody { get; }
    public bool CreateSpace { get; }
}

public interface IManifestWrite
{
    /// <summary>
    /// Create or update full manifest, using details provided in request object
    /// </summary>
    Task<PresUpdateResult> Upsert(UpsertManifestRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Create new manifest, using details provided in request object
    /// </summary>
    Task<PresUpdateResult> Create(WriteManifestRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Service to help with creation of manifests
/// </summary>
public class ManifestWriteService(
    PresentationContext dbContext,
    IdentityManager identityManager,
    IIIIFS3Service iiifS3,
    CanvasPaintingResolver canvasPaintingResolver,
    IPathGenerator pathGenerator,
    SettingsBasedPathGenerator savedManifestPathGenerator,
    DlcsManifestCoordinator dlcsManifestCoordinator,
    IParentSlugParser parentSlugParser,
    IManifestStorageManager manifestStorageManager,
    IPathRewriteParser pathRewriteParser,
    ILogger<ManifestWriteService> logger) : IManifestWrite
{
    /// <summary>
    /// Create or update full manifest, using details provided in request object
    /// </summary>
    public async Task<PresUpdateResult> Upsert(UpsertManifestRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var existingManifest =
                await dbContext.RetrieveManifestAsync(request.CustomerId, request.ManifestId, true,
                    withCanvasPaintings: true, withBatches: true, cancellationToken: cancellationToken);

            if (existingManifest == null)
            {
                if (!string.IsNullOrEmpty(request.Etag)) return UpsertErrorHelper.EtagNotRequired<PresentationManifest>();

                logger.LogDebug("Manifest {ManifestId} for Customer {CustomerId} doesn't exist, creating",
                    request.ManifestId, request.CustomerId);
                return await CreateInternal(request, request.ManifestId, cancellationToken);
            }

            return await UpdateInternal(request, existingManifest, cancellationToken);
        }
        catch (DlcsException ex)
        {
            return UpsertErrorHelper.DlcsError<PresentationManifest>(ex.Message);
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
        catch (DlcsException ex)
        {
            return UpsertErrorHelper.DlcsError<PresentationManifest>(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating manifest with slug '{Slug}' for customer {CustomerId}",
                request.PresentationManifest.Slug, request.CustomerId);
            return PresUpdateResult.Failure("Unexpected error creating manifest", ModifyCollectionType.Unknown,
                WriteResult.Error);
        }
    }

    private async Task<PresUpdateResult> CreateInternal(WriteManifestRequest request, string? manifestId,
        CancellationToken cancellationToken)
    {
        using (logger.BeginScope("Creating Manifest for Customer {CustomerId}", request.CustomerId))
        {
            // retrieve and validate the canvas paintings on the request
            var createdCanvasPaintingRecords =
                await canvasPaintingResolver.GenerateCanvasPaintings(request.CustomerId, request.PresentationManifest,
                    cancellationToken);
            if (createdCanvasPaintingRecords.Error != null) return createdCanvasPaintingRecords.Error;

            // retrieve and validate the parent and slug on the request
            var parsedParentSlugResult =
                await parentSlugParser.Parse(request.PresentationManifest, request.CustomerId, null, cancellationToken); 
            if (parsedParentSlugResult.IsError) return parsedParentSlugResult.Errors;
            var parsedParentSlug = parsedParentSlugResult.ParsedParentSlug;

            // Ensure we have a manifestId
            manifestId ??= await GenerateUniqueManifestId(request, cancellationToken);
            if (manifestId == null) return UpsertErrorHelper.CannotGenerateUniqueId<PresentationManifest>();

            // Carry out any DLCS interactions (for paintedResources with _assets_) 
            var dlcsInteractionResult = await dlcsManifestCoordinator.HandleDlcsInteractions(request, manifestId,
                itemCanvasPaintingsWithAssets: createdCanvasPaintingRecords.CanvasPaintingsThatContainItemsWithAssets,
                cancellationToken: cancellationToken);
            if (dlcsInteractionResult.Error != null) return dlcsInteractionResult.Error;

            // convert and update the canvas paintings from the interim object, to the database format
            var canvasPaintings =
                createdCanvasPaintingRecords.CanvasPaintingsToAdd?.ConvertInterimCanvasPaintings(dlcsInteractionResult.SpaceId) ?? [];
            canvasPaintings.SetAssetsToIngesting(dlcsInteractionResult.IngestedAssets);

            var (error, dbManifest) =
                await CreateDatabaseRecord(request, parsedParentSlug, manifestId, dlcsInteractionResult.SpaceId, 
                    canvasPaintings, cancellationToken);
            if (error != null) return error;
            Debug.Assert(dbManifest != null);

            var hasAssets = request.PresentationManifest.PaintedResources.HasAsset();
            request.PresentationManifest.Items = await SaveToS3(dbManifest, request, hasAssets,
                dlcsInteractionResult.CanBeBuiltUpfront, cancellationToken);

            return await GeneratePresentationSuccessResult(request.PresentationManifest, request.CustomerId, dbManifest,
                hasAssets, dlcsInteractionResult, WriteResult.Created, cancellationToken);
        }
    }

    private async Task<PresUpdateResult> UpdateInternal(UpsertManifestRequest request,
        DbManifest existingManifest, CancellationToken cancellationToken)
    {
        if (!EtagComparer.IsMatch(existingManifest.Etag, request.Etag))
        {
            return UpsertErrorHelper.EtagNonMatching<PresentationManifest>();
        }

        using (logger.BeginScope("Updating Manifest {ManifestId} for Customer {CustomerId}",
                   request.ManifestId, request.CustomerId))
        {
            var existingAssetIds = existingManifest.CanvasPaintings?.Where(cp => cp.AssetId != null)
                .Select(cp => cp.AssetId!).ToList();
            // retrieve, update and validate canvas paintings using the request
            var updatedCanvasPaintingRecords = await canvasPaintingResolver.UpdateCanvasPaintings(request.CustomerId,
                request.PresentationManifest, existingManifest, cancellationToken);
            if (updatedCanvasPaintingRecords.Error != null) return updatedCanvasPaintingRecords.Error;

            // retrieve + validate the parent and slug from the request
            var parsedParentSlugResult = await parentSlugParser.Parse(request.PresentationManifest, request.CustomerId,
                request.ManifestId, cancellationToken);
            if (parsedParentSlugResult.IsError) return parsedParentSlugResult.Errors;
            var parsedParentSlug = parsedParentSlugResult.ParsedParentSlug;

            // Carry out any DLCS interactions (for paintedResources with _assets_) 
            var dlcsInteractionResult = await dlcsManifestCoordinator.HandleDlcsInteractions(request,
                existingManifest.Id, existingAssetIds, existingManifest,
                updatedCanvasPaintingRecords.CanvasPaintingsThatContainItemsWithAssets, cancellationToken);
            if (dlcsInteractionResult.Error != null) return dlcsInteractionResult.Error;

            UpdateCanvasPaintingsAfterDlcsInteractions(existingManifest, updatedCanvasPaintingRecords, dlcsInteractionResult);

            var (error, dbManifest) =
                await UpdateDatabaseRecord(request, parsedParentSlug!, existingManifest, dlcsInteractionResult.SpaceId, cancellationToken);
            if (error != null) return error;
            Debug.Assert(dbManifest != null);

            var hasAssets = request.PresentationManifest.PaintedResources.HasAsset();
            request.PresentationManifest.Items = await SaveToS3(dbManifest, request, hasAssets,
                dlcsInteractionResult.CanBeBuiltUpfront, cancellationToken);

            return await GeneratePresentationSuccessResult(request.PresentationManifest, request.CustomerId, dbManifest,
                hasAssets, dlcsInteractionResult, WriteResult.Updated, cancellationToken);
        }
    }

    private static void UpdateCanvasPaintingsAfterDlcsInteractions(DbManifest existingManifest,
        CanvasPaintingRecords updatedCanvasPaintingRecords, DlcsInteractionResult dlcsInteractionResult)
    {
        existingManifest.CanvasPaintings ??= [];
        
        SpaceHelper.UpdateCanvasPaintings(existingManifest.CanvasPaintings, dlcsInteractionResult.SpaceId);
        
        var canvasPaintings =
            updatedCanvasPaintingRecords.CanvasPaintingsToAdd?.ConvertInterimCanvasPaintings(dlcsInteractionResult
                .SpaceId) ?? [];
        existingManifest.CanvasPaintings.AddRange(canvasPaintings);
        existingManifest.CanvasPaintings.SetAssetsToIngesting(dlcsInteractionResult.IngestedAssets);
    }

    private async Task<PresUpdateResult> GeneratePresentationSuccessResult(PresentationManifest presentationManifest,
        int customerId, DbManifest dbManifest, bool hasAssets, DlcsInteractionResult dlcsInteractionResult,
        WriteResult writeResult, CancellationToken cancellationToken)
    {
        return PresUpdateResult.Success(
            presentationManifest.SetGeneratedFields(dbManifest, pathGenerator, savedManifestPathGenerator,
                await dlcsManifestCoordinator.GetAssets(customerId, dbManifest, cancellationToken)),
            hasAssets && !dlcsInteractionResult.CanBeBuiltUpfront
                ? WriteResult.Accepted
                : writeResult,
            dbManifest?.Etag);
    }

    private async Task<(PresUpdateResult?, DbManifest?)> CreateDatabaseRecord(WriteManifestRequest request,
        ParsedParentSlug parsedParentSlug, string manifestId, int? spaceId, 
        List<Models.Database.CanvasPainting> canvasPaintings, CancellationToken cancellationToken)
    {
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
                    Slug = parsedParentSlug.Slug,
                    Canonical = true,
                    Type = ResourceType.IIIFManifest,
                    Parent = parsedParentSlug.Parent.Id
                }
            ],
            CanvasPaintings = canvasPaintings,
            SpaceId = spaceId,
        };

        await dbContext.AddAsync(dbManifest, cancellationToken);

        var saveErrors = await SaveAndPopulateEntity(request, dbManifest, cancellationToken);
        return (saveErrors, dbManifest);
    }

    private async Task<(PresUpdateResult?, DbManifest?)> UpdateDatabaseRecord(WriteManifestRequest request,
        ParsedParentSlug parsedParentSlug, DbManifest existingManifest, int? dlcsInteractionResultSpace, CancellationToken cancellationToken)
    {
        existingManifest.Label = request.PresentationManifest.Label;

        existingManifest.Modified = DateTime.UtcNow;
        existingManifest.ModifiedBy = Authorizer.GetUser();

        var canonicalHierarchy = existingManifest.Hierarchy!.Single(c => c.Canonical);
        canonicalHierarchy.Slug = parsedParentSlug.Slug;
        canonicalHierarchy.Parent = parsedParentSlug.Parent.Id;

        existingManifest.SpaceId ??= dlcsInteractionResultSpace;

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

    /// <summary>
    /// Saves a manifest into S3
    /// </summary>
    /// <param name="dbManifest">The manifest record</param>
    /// <param name="request">The request made by the caller</param>
    /// <param name="hasAssets">
    /// Whether there are any assets identified in the request
    ///
    /// TThis is relevant for both painted resources and assets from items
    /// </param>
    /// <param name="canBeBuiltUpfront">
    /// Whether there's assets, but they're all tracked by the DLCS
    ///
    /// This is only relevant for painted resources
    /// </param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A list of canvases to be returned to the caller</returns>
    private async Task<List<Canvas>?> SaveToS3(DbManifest dbManifest, WriteManifestRequest request, bool hasAssets,
        bool canBeBuiltUpfront, CancellationToken cancellationToken)
    {
        var iiifManifest = request.RawRequestBody.FromJson<IIIF.Presentation.V3.Manifest>();
        
        if (canBeBuiltUpfront && hasAssets)
        {
            var manifest = await manifestStorageManager.UpsertManifestInStorage(iiifManifest, dbManifest, cancellationToken);
            request.PresentationManifest.Items = manifest.Items;
        }
        else
        {
            // There are assets that aren't tracked by the DLCS, so set provisional canvases while further processing
            // happens in the background handler
            if (hasAssets)
            {
                var canvasPaintings =  dbManifest.CanvasPaintings;
                
                if (canvasPaintings is not null)
                {
                    iiifManifest.Items =
                        canvasPaintings.GenerateProvisionalCanvases(savedManifestPathGenerator, iiifManifest.Items,
                            pathRewriteParser);
                }
            }

            await manifestStorageManager.SaveManifestInStorage(iiifManifest, dbManifest, hasAssets,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        
        return iiifManifest.Items;
    }
}
