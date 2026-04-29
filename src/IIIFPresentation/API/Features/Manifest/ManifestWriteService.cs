using System.Data;
using System.Diagnostics;
using API.Converters;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Helpers;
using API.Infrastructure.IdGenerator;
using Core;
using Core.Auth;
using Core.IIIF;
using DLCS.Exceptions;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Models.API.General;
using Models.API.Manifest;
using Models.Database;
using Models.DLCS;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using Services.Manifests.AWS;
using Services.Manifests.Helpers;
using Services.Manifests.Model;
using CanvasPainting = Models.Database.CanvasPainting;
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
                await dbContext.RetrieveManifestAsync(request.ManifestId, true,
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
            var resolved = await ResolveCanvasPaintingsAndParentSlug(request, cancellationToken: cancellationToken);
            if (resolved.Error != null) return resolved.Error;

            // Ensure we have a manifestId
            manifestId ??= await GenerateUniqueManifestId(request, cancellationToken);
            if (manifestId == null) return UpsertErrorHelper.CannotGenerateUniqueId<PresentationManifest>();

            // Carry out any DLCS interactions and update canvas paintings
            var dlcsResult = await HandleDlcsInteractions(request, manifestId, resolved.ParsedManifestResult!,
                cancellationToken: cancellationToken);
            if (dlcsResult.Error != null) return dlcsResult.Error;

            var (error, dbManifest) =
                await CreateDatabaseRecord(request, resolved.ParsedParentSlug!, manifestId, dlcsResult.InteractionResult!.SpaceId,
                    dlcsResult.CanvasPaintings, cancellationToken);
            if (error != null) return error;

            return await SaveToS3AndGenerateResult(request, dbManifest!, dlcsResult.InteractionResult!, WriteResult.Created,
                cancellationToken);
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
            var resolved = await ResolveCanvasPaintingsAndParentSlug(request, request.ManifestId, existingManifest, cancellationToken);
            if (resolved.Error != null) return resolved.Error;

            // Carry out any DLCS interactions and update canvas paintings
            var dlcsResult = await HandleDlcsInteractions(request, existingManifest.Id, resolved.ParsedManifestResult!, existingAssetIds, existingManifest, cancellationToken);
            if (dlcsResult.Error != null) return dlcsResult.Error;

            var (error, dbManifest) = await UpdateDatabaseRecord(request, resolved.ParsedParentSlug!, existingManifest,
                dlcsResult.InteractionResult!.SpaceId, cancellationToken);
            if (error != null) return error;

            return await SaveToS3AndGenerateResult(request, dbManifest!, dlcsResult.InteractionResult!, WriteResult.Updated,
                cancellationToken);
        }
    }

    private async Task<DlcsHandleResult> HandleDlcsInteractions(WriteManifestRequest request, string manifestId,
        ParsedManifestResult canvasPaintingRecords, List<AssetId>? existingAssetIds = null, DbManifest? existingManifest = null, 
        CancellationToken cancellationToken = default)
    {
        var dlcsResult = await dlcsManifestCoordinator.HandleDlcsInteractions(request, manifestId,
            existingAssetIds, existingManifest,
            canvasPaintingRecords.CanvasPaintingsThatContainItemsWithAssets, canvasPaintingRecords.AdjunctInteractions, cancellationToken);
        if (dlcsResult.Error != null) return DlcsHandleResult.Failure(dlcsResult.Error);

        if (existingManifest == null)
        {
            return DlcsHandleResult.Success(dlcsResult, UpdateCanvasPaintingsAfterDlcsInteractions([],
                canvasPaintingRecords.CanvasPaintingsToAdd, dlcsResult));
        }

        UpdateCanvasPaintingsAfterDlcsInteractionsForUpdate(existingManifest, canvasPaintingRecords, dlcsResult);
        return DlcsHandleResult.Success(dlcsResult, existingManifest.CanvasPaintings!);

    }

    private void UpdateCanvasPaintingsAfterDlcsInteractionsForUpdate(DbManifest existingManifest, 
        ParsedManifestResult updatedParsedManifestResult, DlcsInteractionResult dlcsInteractionResult)
    {
        existingManifest.CanvasPaintings ??= [];
        
        SpaceHelper.UpdateCanvasPaintings(existingManifest.CanvasPaintings, dlcsInteractionResult.SpaceId);

        UpdateCanvasPaintingsAfterDlcsInteractions(existingManifest.CanvasPaintings,
            updatedParsedManifestResult.CanvasPaintingsToAdd, dlcsInteractionResult);
    }

    private List<CanvasPainting> UpdateCanvasPaintingsAfterDlcsInteractions(List<CanvasPainting> initialCanvasPaintings,
        List<InterimCanvasPainting>? interimCanvasPaintings, DlcsInteractionResult dlcsInteractionResult)
    {
        var convertedCanvasPaintings =
            interimCanvasPaintings?.ConvertInterimCanvasPaintings(dlcsInteractionResult.SpaceId) ?? [];
        initialCanvasPaintings.AddRange(convertedCanvasPaintings);
        initialCanvasPaintings.SetAssetsToIngesting(dlcsInteractionResult.IngestedAssets);
        
        return initialCanvasPaintings;
    }

    private async Task<ResolvedManifestData> ResolveCanvasPaintingsAndParentSlug(WriteManifestRequest request,
        string? manifestId = null, DbManifest? existingManifest = null, CancellationToken cancellationToken = default)
    {
        var (canvasError, canvasPaintingRecords) = await ResolveCanvasPaintings(request, existingManifest, cancellationToken);
        if (canvasError != null) return ResolvedManifestData.Failure(canvasError);

        var (slugError, parsedParentSlug) = await ParseParentSlug(request, manifestId, cancellationToken);
        if (slugError != null) return ResolvedManifestData.Failure(slugError);

        return ResolvedManifestData.Success(canvasPaintingRecords!, parsedParentSlug!);
    }

    private async Task<(PresUpdateResult? error, ParsedManifestResult? records)> ResolveCanvasPaintings(
        WriteManifestRequest request, DbManifest? existingManifest, CancellationToken cancellationToken)
    {
        var isCreate = existingManifest == null;
        
        var result = isCreate
            ? await canvasPaintingResolver.GenerateCanvasPaintings(request.CustomerId, request.PresentationManifest,
                cancellationToken)
            : await canvasPaintingResolver.UpdateCanvasPaintings(request.CustomerId, request.PresentationManifest,
                existingManifest!, cancellationToken);
        return result.Error != null ? (result.Error, null) : (null, result);
    }

    private async Task<(PresUpdateResult? error, ParsedParentSlug? parsedParentSlug)> ParseParentSlug(
        WriteManifestRequest request, string? manifestId, CancellationToken cancellationToken)
    {
        var result = await parentSlugParser.Parse(request.PresentationManifest, request.CustomerId, manifestId,
            cancellationToken);
        return result.IsError ? (result.Errors, null) : (null, result.ParsedParentSlug);
    }

    private async Task<PresUpdateResult> SaveToS3AndGenerateResult(WriteManifestRequest request, DbManifest dbManifest,
        DlcsInteractionResult dlcsInteractionResult, WriteResult writeResult, CancellationToken cancellationToken)
    {
        var hasAssets = request.PresentationManifest.PaintedResources.HasAsset();
        request.PresentationManifest.Items = await SaveToS3(dbManifest, request, hasAssets,
            dlcsInteractionResult.CanBeBuiltUpfront, cancellationToken);
        return await GeneratePresentationSuccessResult(request.PresentationManifest, request.CustomerId, dbManifest,
            hasAssets, dlcsInteractionResult, writeResult, cancellationToken);
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
        ParsedParentSlug parsedParentSlug, DbManifest existingManifest, int? manifestSpace, CancellationToken cancellationToken)
    {
        existingManifest.Label = request.PresentationManifest.Label;

        existingManifest.Modified = DateTime.UtcNow;
        existingManifest.ModifiedBy = Authorizer.GetUser();

        var canonicalHierarchy = existingManifest.Hierarchy!.Single(c => c.Canonical);
        canonicalHierarchy.Slug = parsedParentSlug.Slug;
        canonicalHierarchy.Parent = parsedParentSlug.Parent.Id;

        // set the space id if it's not been set previously - this will be null still if there's been no space created
        existingManifest.SpaceId ??= manifestSpace;

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

    /// <summary>
    /// Contains results of a manifest that has been parsed into the format iiif-presentation understands
    /// </summary>
    private class ResolvedManifestData
    {
        /// <summary>
        /// If canvas painting resolution or slug parsing failed.
        /// </summary>
        public PresUpdateResult? Error { get; private init; }
        /// <summary>
        /// Canvas paintings resolved from the request
        /// </summary>
        public ParsedManifestResult? ParsedManifestResult { get; private init; }
        /// <summary>
        /// Parsed parent and slug from the request hierarchy path.
        /// </summary>
        public ParsedParentSlug? ParsedParentSlug { get; private init; }

        public static ResolvedManifestData Failure(PresUpdateResult error) => new() { Error = error };

        public static ResolvedManifestData Success(ParsedManifestResult records, ParsedParentSlug slug) =>
            new() { ParsedManifestResult = records, ParsedParentSlug = slug };
    }

    /// <summary>
    /// Contains results following DLCS interactions
    /// </summary>
    private class DlcsHandleResult
    {
        /// <summary>
        /// If the DLCS interaction or canvas painting update failed.
        /// </summary>
        public PresUpdateResult? Error { get; private init; }
        /// <summary>
        /// Result of the DLCS interaction, including space ID and ingested asset IDs.
        /// </summary>
        public DlcsInteractionResult? InteractionResult { get; private init; }
        /// <summary>
        /// Final canvas paintings to persist, updated with DLCS space and ingest state.
        /// </summary>
        public List<CanvasPainting> CanvasPaintings { get; private init; } = [];

        public static DlcsHandleResult Failure(PresUpdateResult error) => new() { Error = error };

        public static DlcsHandleResult Success(DlcsInteractionResult interactionResult, List<CanvasPainting> canvasPaintings) =>
            new() { InteractionResult = interactionResult, CanvasPaintings = canvasPaintings };
    }
}
