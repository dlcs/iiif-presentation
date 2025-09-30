using System.Data;
using API.Converters;
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
using Repository;
using Repository.Helpers;
using Repository.Paths;
using Services.Manifests.AWS;
using Services.Manifests.Helpers;
using Services.Manifests.Model;
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
                if (!string.IsNullOrEmpty(request.Etag)) return ErrorHelper.EtagNotRequired<PresentationManifest>();

                logger.LogDebug("Manifest {ManifestId} for Customer {CustomerId} doesn't exist, creating",
                    request.ManifestId, request.CustomerId);
                return await CreateInternal(request, request.ManifestId, cancellationToken);
            }

            return await UpdateInternal(request, existingManifest, cancellationToken);
        }
        catch (DlcsException ex)
        {
            return ErrorHelper.DlcsError<PresentationManifest>(ex.Message);
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
            return ErrorHelper.DlcsError<PresentationManifest>(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating manifest with slug '{Slug}' for customer {CustomerId}",
                request.PresentationManifest.Slug, request.CustomerId);
            return PresUpdateResult.Failure("Unexpected error creating manifest", ModifyCollectionType.Unknown,
                WriteResult.Error);
        }
    }

    private async Task<PresUpdateResult> CreateInternal(WriteManifestRequest request, string? manifestId, CancellationToken cancellationToken)
    {
        using (logger.BeginScope("Creating Manifest for Customer {CustomerId}", request.CustomerId))
        {
            // retrieve and validate the canvas paintings on the request
            var (canvasPaintingsError, interimCanvasPaintings) =
                await canvasPaintingResolver.GenerateCanvasPaintings(request.CustomerId, request.PresentationManifest,
                    cancellationToken);
            if (canvasPaintingsError != null) return canvasPaintingsError;
            
            // retrieve and validate the parent and slug on the request
            var parsedParentSlugResult =
                await parentSlugParser.Parse(request.PresentationManifest, request.CustomerId, null, cancellationToken);
            if (parsedParentSlugResult.IsError) return parsedParentSlugResult.Errors;
            var parsedParentSlug = parsedParentSlugResult.ParsedParentSlug;
            
            // Ensure we have a manifestId
            manifestId ??= await GenerateUniqueManifestId(request, cancellationToken);
            if (manifestId == null) return ErrorHelper.CannotGenerateUniqueId<PresentationManifest>();
            
            // Carry out any DLCS interactions (for paintedResources with _assets_) 
            var dlcsInteractionResult = await dlcsManifestCoordinator.HandleDlcsInteractions(request, manifestId,
                itemCanvasPaintingsWithAssets: interimCanvasPaintings?.Where(icp =>
                    icp is { SuspectedAssetId: not null, CanvasPaintingType: CanvasPaintingType.Items }).ToList(),
                cancellationToken: cancellationToken);
            if (dlcsInteractionResult.Error != null) return dlcsInteractionResult.Error;
            
            // convert and update the canvas paintings from the interim object, to the database format
            var canvasPaintings = interimCanvasPaintings?.ConvertInterimCanvasPaintings(dlcsInteractionResult.SpaceId) ?? [];
            canvasPaintings.SetAssetsToIngesting(dlcsInteractionResult.IngestedAssets);
            
            var (error, dbManifest) =
                await CreateDatabaseRecord(request, parsedParentSlug, manifestId, dlcsInteractionResult.SpaceId, dlcsInteractionResult, canvasPaintings, cancellationToken);
            if (error != null) return error;
            
            var hasAssets = request.PresentationManifest.PaintedResources.HasAsset();
            request.PresentationManifest.Items = await SaveToS3(dbManifest!, request, hasAssets,
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
            return ErrorHelper.EtagNonMatching<PresentationManifest>();
        }

        using (logger.BeginScope("Updating Manifest {ManifestId} for Customer {CustomerId}",
                   request.ManifestId, request.CustomerId))
        {
            var existingAssetIds = existingManifest.CanvasPaintings?.Where(cp => cp.AssetId != null)
                .Select(cp => cp.AssetId!).ToList();
            // retrieve, update and validate canvas paintings using the request
            var (canvasPaintingsError, interimCanvasPaintingsToAdd) = await canvasPaintingResolver.UpdateCanvasPaintings(request.CustomerId,
                request.PresentationManifest, existingManifest, cancellationToken);
            if (canvasPaintingsError != null) return canvasPaintingsError;
            
            // retrieve + validate the parent and slug from the request
            var parsedParentSlugResult = await parentSlugParser.Parse(request.PresentationManifest, request.CustomerId,
                request.ManifestId, cancellationToken);
            if (parsedParentSlugResult.IsError) return parsedParentSlugResult.Errors;
            var parsedParentSlug = parsedParentSlugResult.ParsedParentSlug;
            
            // Carry out any DLCS interactions (for paintedResources with _assets_) 
            var dlcsInteractionResult = await dlcsManifestCoordinator.HandleDlcsInteractions(request,
                existingManifest.Id,  existingAssetIds, existingManifest,
                interimCanvasPaintingsToAdd?.Where(icp => icp is
                    { SuspectedAssetId: not null, CanvasPaintingType: CanvasPaintingType.Items }).ToList(), cancellationToken);
            if (dlcsInteractionResult.Error != null) return dlcsInteractionResult.Error;
            
            // update existing manifest with canvas paintings following DLCS interactions
            var canvasPaintings = interimCanvasPaintingsToAdd?.ConvertInterimCanvasPaintings(dlcsInteractionResult.SpaceId) ?? [];
            existingManifest.CanvasPaintings ??= [];
            existingManifest.CanvasPaintings.AddRange(canvasPaintings);
            existingManifest.CanvasPaintings.SetAssetsToIngesting(dlcsInteractionResult.IngestedAssets);
            
            var (error, dbManifest) =
                await UpdateDatabaseRecord(request, parsedParentSlug!, existingManifest, dlcsInteractionResult, cancellationToken);
            if (error != null) return error;
            
            var hasAssets = request.PresentationManifest.PaintedResources.HasAsset();
            request.PresentationManifest.Items = await SaveToS3(dbManifest!, request, hasAssets,
                dlcsInteractionResult.CanBeBuiltUpfront, cancellationToken);

            return await GeneratePresentationSuccessResult(request.PresentationManifest, request.CustomerId, dbManifest,
                hasAssets, dlcsInteractionResult, WriteResult.Updated, cancellationToken);
        }
    }

    private async Task<PresUpdateResult> GeneratePresentationSuccessResult(PresentationManifest presentationManifest, 
        int customerId, DbManifest? dbManifest, bool hasAssets, DlcsInteractionResult dlcsInteractionResult, 
        WriteResult writeResult, CancellationToken cancellationToken)
    {
        return PresUpdateResult.Success(
            presentationManifest.SetGeneratedFields(dbManifest!, pathGenerator,
                await dlcsManifestCoordinator.GetAssets(customerId, dbManifest, cancellationToken)),
            hasAssets && !dlcsInteractionResult.CanBeBuiltUpfront
                ? WriteResult.Accepted
                : writeResult,
            dbManifest?.Etag);
    }

    private async Task<(PresUpdateResult?, DbManifest?)> CreateDatabaseRecord(WriteManifestRequest request,
        ParsedParentSlug parsedParentSlug, string manifestId, int? spaceId, DlcsInteractionResult dlcsInteractionResult,
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
            LastProcessed = RequiresFurtherProcessing(dlcsInteractionResult) ? null : timeStamp,
        };

        await dbContext.AddAsync(dbManifest, cancellationToken);

        var saveErrors = await SaveAndPopulateEntity(request, dbManifest, cancellationToken);
        return (saveErrors, dbManifest);
    }

    /// <summary>
    /// Manifest doesn't require further processing in two scenarios:
    /// 1. Determined that there's no DLCS interaction required at all (no new space and no assets)
    /// 2. Determined that there are no unchecked assets (after checking with DLCS)
    /// </summary>
    private static bool RequiresFurtherProcessing(DlcsInteractionResult dlcsInteractionResult) =>
        dlcsInteractionResult != DlcsInteractionResult.NoInteraction &&
        dlcsInteractionResult is { OnlySpace: false, CanBeBuiltUpfront: false };

    private async Task<(PresUpdateResult?, DbManifest?)> UpdateDatabaseRecord(WriteManifestRequest request,
        ParsedParentSlug parsedParentSlug, DbManifest existingManifest, DlcsInteractionResult dlcsInteractionResult,
        CancellationToken cancellationToken)
    {
        existingManifest.Label = request.PresentationManifest.Label;
        
        existingManifest.Modified = DateTime.UtcNow;
        existingManifest.ModifiedBy = Authorizer.GetUser();

        if (!RequiresFurtherProcessing(dlcsInteractionResult))
        {
            existingManifest.LastProcessed = DateTime.UtcNow;
        }
        // else: BackgroundHandler will set the value
        
        var canonicalHierarchy = existingManifest.Hierarchy!.Single(c => c.Canonical);
        canonicalHierarchy.Slug = parsedParentSlug.Slug;
        canonicalHierarchy.Parent = parsedParentSlug.Parent.Id;

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

    private async Task<List<Canvas>?> SaveToS3(DbManifest dbManifest, WriteManifestRequest request, bool hasAssets,
        bool canBeBuiltUpfront, CancellationToken cancellationToken)
    {
        var iiifManifest = request.RawRequestBody.FromJson<IIIF.Presentation.V3.Manifest>();

        if (canBeBuiltUpfront)
        {
            var manifest = await manifestStorageManager.UpsertManifestInStorage(iiifManifest, dbManifest, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            request.PresentationManifest.Items = manifest.Items;
        }
        else
        {
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
            
            await iiifS3.SaveIIIFToS3(iiifManifest, dbManifest, pathGenerator.GenerateFlatManifestId(dbManifest),
                hasAssets, cancellationToken);
        }

        return iiifManifest.Items;
    }
}
