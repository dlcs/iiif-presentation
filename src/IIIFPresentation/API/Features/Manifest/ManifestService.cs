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
using IIIF.Serialisation;
using Models.API.Manifest;
using Models.Database.General;
using Repository;
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
    IIdGenerator idGenerator,
    IIIFS3Service iiifS3,
    IETagManager eTagManager,
    ILogger<ManifestService> logger)
{
    public async Task<PresUpdateResult> Upsert(UpsertManifestRequest request, CancellationToken cancellationToken)
    {
        var existingManifest =
            await dbContext.Manifests.Retrieve(request.CustomerId, request.ManifestId, true, cancellationToken);

        if (existingManifest == null)
        {
            if (!string.IsNullOrEmpty(request.Etag)) return ErrorHelper.EtagNotRequired<PresentationManifest>();
            
            logger.LogDebug("Manifest {ManifestId} for Customer {CustomerId} doesn't exist, creating",
                request.ManifestId, request.CustomerId);
            return await CreateInternal(request, request.ManifestId, cancellationToken);
        }
        
        return await UpdateInternal(request, existingManifest, cancellationToken);
    }

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

    private async Task<(PresUpdateResult? parentErrors, Collection? parentCollection)> TryGetParent(
        WriteManifestRequest request, CancellationToken cancellationToken)
    {
        var manifest = request.PresentationManifest;
        var urlRoots = request.UrlRoots;;
        var parentCollection = await dbContext.Collections.Retrieve(request.CustomerId,
            manifest.GetParentSlug(), cancellationToken: cancellationToken);
        
        // Validation
        if (parentCollection == null) return (ErrorHelper.NullParentResponse<PresentationManifest>(), null);
        if (!parentCollection.IsStorageCollection) return (ManifestErrorHelper.ParentMustBeStorageCollection<PresentationManifest>(), null);
        if (manifest.IsUriParentInvalid(parentCollection, urlRoots)) return (ErrorHelper.NullParentResponse<PresentationManifest>(), null);

        return (null, parentCollection);
    }
    
    private async Task<(PresUpdateResult?, DbManifest?)> CreateDatabaseRecord(
        WriteManifestRequest request, Collection parentCollection, string? requestedId, CancellationToken cancellationToken)
    {
        var id = requestedId ?? await GenerateUniqueId(request, cancellationToken);
        if (id == null) return (ErrorHelper.CannotGenerateUniqueId<PresentationManifest>(), null);

        var timeStamp = DateTime.UtcNow;
        var dbManifest = new DbManifest
        {
            Id = id,
            CustomerId = request.CustomerId,
            Created = timeStamp,
            Modified = timeStamp,
            CreatedBy = Authorizer.GetUser(),
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = request.PresentationManifest.Slug!,
                    Canonical = true,
                    Type = ResourceType.IIIFManifest,
                    Parent = parentCollection.Id,
                }
            ]
        };
        
        dbContext.Add(dbManifest);
        
        // TODO Everything below here is shared - only the actual DB work that differs
        var saveErrors =
            await dbContext.TrySave<PresentationManifest>("manifest", request.CustomerId, logger, cancellationToken);

        return (saveErrors, dbManifest);
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

        logger.LogDebug("Manifest {ManifestId} for Customer {CustomerId} exists, updating", request.ManifestId,
            request.CustomerId);

        var (error, dbManifest) =
            await UpdateDatabaseRecord(request, parentCollection!, existingManifest, cancellationToken);
        if (error != null) return error; 

        await SaveToS3(dbManifest!, request, cancellationToken);
        
        return PresUpdateResult.Success(
            request.PresentationManifest.SetGeneratedFields(dbManifest!, request.UrlRoots), WriteResult.Updated);
    }
    
    private async Task<(PresUpdateResult?, DbManifest?)> UpdateDatabaseRecord(
        WriteManifestRequest request,  Collection parentCollection, DbManifest existingManifest,CancellationToken cancellationToken)
    {
        existingManifest.Modified = DateTime.UtcNow;
        existingManifest.ModifiedBy = Authorizer.GetUser();
        var canonicalHierarchy = existingManifest.Hierarchy!.Single(c => c.Canonical);
        
        // TODO - are these allowed?
        canonicalHierarchy.Slug = request.PresentationManifest.Slug!;
        canonicalHierarchy.Parent = parentCollection.Id;
        
        var saveErrors =
            await dbContext.TrySave<PresentationManifest>("manifest", request.CustomerId, logger, cancellationToken);

        return (saveErrors, existingManifest);
    }

    private async Task<string?> GenerateUniqueId(WriteManifestRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Manifests.GenerateUniqueIdAsync(request.CustomerId, idGenerator, cancellationToken);
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
        await iiifS3.SaveIIIFToS3(iiifManifest, dbManifest, dbManifest.GenerateFlatManifestId(request.UrlRoots),
            cancellationToken);
    }
}