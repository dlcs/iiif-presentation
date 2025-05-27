using System.Data;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Helpers;
using API.Infrastructure.IdGenerator;
using AWS.Helpers;
using Core;
using Core.Auth;
using Core.Helpers;
using Core.IIIF;
using DLCS.Exceptions;
using IIIF.Serialisation;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Repository.Paths;
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
    IIIFS3Service iiifS3,
    IETagManager eTagManager,
    CanvasPaintingResolver canvasPaintingResolver,
    IPathGenerator pathGenerator,
    IManifestRead manifestRead,
    DlcsManifestCoordinator dlcsManifestCoordinator,
    IParentSlugParser parentSlugParser,
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
            logger.LogError(ex, "Error creating manifest with slug '{Slug}' for customer {CustomerId}",
                request.PresentationManifest.Slug, request.CustomerId);
            return PresUpdateResult.Failure("Unexpected error creating manifest", ModifyCollectionType.Unknown,
                WriteResult.Error);
        }
    }

    private async Task<PresUpdateResult> CreateInternal(WriteManifestRequest request, string? manifestId, CancellationToken cancellationToken)
    {
        var parsedParentSlugResult =
            await parentSlugParser.Parse(request.PresentationManifest, request.CustomerId, null, cancellationToken);
        if (parsedParentSlugResult.IsError) return parsedParentSlugResult.Errors;
        var parsedParentSlug = parsedParentSlugResult.ParsedParentSlug;

        var saveToStaging = ShouldSaveToStaging(request);
        // can't have both items and painted resources, so items take precedence
        if (!request.PresentationManifest.Items.IsNullOrEmpty())
        {
            request.PresentationManifest.PaintedResources = null;
        }

        using (logger.BeginScope("Creating Manifest for Customer {CustomerId}", request.CustomerId))
        {
            // Ensure we have a manifestId
            manifestId ??= await GenerateUniqueManifestId(request, cancellationToken);
            if (manifestId == null) return ErrorHelper.CannotGenerateUniqueId<PresentationManifest>();
            
            // Carry out any DLCS interactions (for paintedResources with _assets_) 
            var dlcsInteractionResult =
                await dlcsManifestCoordinator.HandleDlcsInteractions(request, manifestId, cancellationToken: cancellationToken);
            if (dlcsInteractionResult.Error != null) return dlcsInteractionResult.Error;

            var (error, dbManifest) =
                await CreateDatabaseRecord(request, parsedParentSlug, manifestId, dlcsInteractionResult.SpaceId, cancellationToken);
            if (error != null) return error;

            await SaveToS3(dbManifest!, request, saveToStaging, cancellationToken);
            
            return PresUpdateResult.Success(
                request.PresentationManifest.SetGeneratedFields(dbManifest!, pathGenerator,
                    await manifestRead.GetAssets(request.CustomerId, dbManifest, cancellationToken)),
                request.PresentationManifest.PaintedResources.HasAsset() ? WriteResult.Accepted : WriteResult.Created);
        }
    }

    private static bool ShouldSaveToStaging(WriteManifestRequest request)
    {
        return request.PresentationManifest.PaintedResources?.Any(x => x.Asset != null) == true;
    }

    private async Task<PresUpdateResult> UpdateInternal(UpsertManifestRequest request,
        DbManifest existingManifest, CancellationToken cancellationToken)
    {
        if (!eTagManager.TryGetETag(existingManifest, out var eTag) || eTag != request.Etag)
        {
            return ErrorHelper.EtagNonMatching<PresentationManifest>();
        }
        
        var hasAsset = request.PresentationManifest.PaintedResources.HasAsset();
        var noBatches = existingManifest.Batches.IsNullOrEmpty();

        if (existingManifest.CanvasPaintings?.Count > 0)
        {
            if (!hasAsset && !noBatches)
            {
                return ErrorHelper.ManifestCreatedWithAssetsCannotBeUpdatedWithItems<PresentationManifest>();
            }
            
            if (hasAsset && noBatches)
            {
                return ErrorHelper.ManifestCreatedWithItemsCannotBeUpdatedWithAssets<PresentationManifest>();
            }
        }

        var parsedParentSlugResult = await parentSlugParser.Parse(request.PresentationManifest, request.CustomerId,
            request.ManifestId, cancellationToken);
        if (parsedParentSlugResult.IsError) return parsedParentSlugResult.Errors;
        var parsedParentSlug = parsedParentSlugResult.ParsedParentSlug;

        using (logger.BeginScope("Updating Manifest {ManifestId} for Customer {CustomerId}",
                   request.ManifestId, request.CustomerId))
        {
            // Carry out any DLCS interactions (for paintedResources with _assets_) 
            var dlcsInteractionResult =
                await dlcsManifestCoordinator.HandleDlcsInteractions(request, existingManifest.Id, existingManifest, cancellationToken);
            if (dlcsInteractionResult.Error != null) return dlcsInteractionResult.Error;
            
            var (error, dbManifest) =
                await UpdateDatabaseRecord(request, parsedParentSlug!, existingManifest, cancellationToken);
            if (error != null) return error;

            var saveToStaging = ShouldSaveToStaging(request);
            await SaveToS3(dbManifest!, request, saveToStaging, cancellationToken);

            return PresUpdateResult.Success(
                request.PresentationManifest.SetGeneratedFields(dbManifest!, pathGenerator, 
                    await manifestRead.GetAssets(request.CustomerId, dbManifest, cancellationToken)),
                request.PresentationManifest.PaintedResources.HasAsset() ? WriteResult.Accepted : WriteResult.Updated);
        }
    }

    private async Task<(PresUpdateResult?, DbManifest?)> CreateDatabaseRecord(WriteManifestRequest request,
        ParsedParentSlug parsedParentSlug, string manifestId, int? spaceId, CancellationToken cancellationToken)
    {
        var (canvasPaintingsError, canvasPaintings) =
            await canvasPaintingResolver.GenerateCanvasPaintings(request.CustomerId, request.PresentationManifest,
                cancellationToken);
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
                    Slug = parsedParentSlug.Slug,
                    Canonical = true,
                    Type = ResourceType.IIIFManifest,
                    Parent = parsedParentSlug.Parent.Id
                }
            ],
            CanvasPaintings = canvasPaintings,
            SpaceId = spaceId,
            LastProcessed = RequiresFurtherProcessing(request.PresentationManifest) ? null : timeStamp,
        };

        await dbContext.AddAsync(dbManifest, cancellationToken);

        var saveErrors = await SaveAndPopulateEntity(request, dbManifest, cancellationToken);
        return (saveErrors, dbManifest);
    }

    /// <summary>
    /// Check if manifest will require further processing. This is used to set .LastProcessed for a manifest. If further
    /// processing is required this later processing will trigger update to field.
    /// </summary>
    private static bool RequiresFurtherProcessing(PresentationManifest presentationManifest) =>
        presentationManifest.PaintedResources?.Any(pr => pr.Asset != null) ?? false;

    private async Task<(PresUpdateResult?, DbManifest?)> UpdateDatabaseRecord(WriteManifestRequest request,
        ParsedParentSlug parsedParentSlug, DbManifest existingManifest, CancellationToken cancellationToken)
    {
        var presentationManifest = request.PresentationManifest;
        var canvasPaintingsError = await canvasPaintingResolver.UpdateCanvasPaintings(request.CustomerId,
            presentationManifest, existingManifest, cancellationToken);
        if (canvasPaintingsError != null) return (canvasPaintingsError, null);
        
        existingManifest.Modified = DateTime.UtcNow;
        existingManifest.ModifiedBy = Authorizer.GetUser();
        existingManifest.Label = presentationManifest.Label;
        existingManifest.LastProcessed = RequiresFurtherProcessing(presentationManifest) ? null : DateTime.UtcNow;
        
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

    private async Task SaveToS3(DbManifest dbManifest, WriteManifestRequest request, bool saveToStaging,
        CancellationToken cancellationToken)
    {
        var iiifManifest = request.RawRequestBody.FromJson<IIIF.Presentation.V3.Manifest>();
 
        await iiifS3.SaveIIIFToS3(iiifManifest, dbManifest, pathGenerator.GenerateFlatManifestId(dbManifest),
            saveToStaging, cancellationToken);
    }
}
