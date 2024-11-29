﻿using System.Data;
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
using IIIF.Serialisation;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.General;
using Repository;
using Repository.Helpers;
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
    string RawRequestBody) : WriteManifestRequest(CustomerId, PresentationManifest, RawRequestBody);

/// <summary>
/// Record containing fields for creating a Manifest
/// </summary>
public record WriteManifestRequest(
    int CustomerId,
    PresentationManifest PresentationManifest,
    string RawRequestBody);

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
                await CreateDatabaseRecord(request, parentCollection!, manifestId, cancellationToken);
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

    private async Task<(PresUpdateResult?, DbManifest?)> CreateDatabaseRecord(WriteManifestRequest request,
        Collection parentCollection, string? requestedId, CancellationToken cancellationToken)
    {
        var id = requestedId ?? await GenerateUniqueManifestId(request, cancellationToken);
        if (id == null) return (ErrorHelper.CannotGenerateUniqueId<PresentationManifest>(), null);

        var (canvasPaintingsError, canvasPaintings) =
            await canvasPaintingResolver.InsertCanvasPaintings(request.CustomerId, request.PresentationManifest,
                cancellationToken);
        if (canvasPaintingsError != null) return (canvasPaintingsError, null);
        
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