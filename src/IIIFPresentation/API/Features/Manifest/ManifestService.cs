using System.Data;
using API.Auth;
using API.Converters;
using API.Features.Manifest.Helpers;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.AWS;
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

public record UpsertManifestRequest(
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
    ILogger<ManifestService> logger)
{
    public async Task<PresUpdateResult> Upsert(UpsertManifestRequest request, CancellationToken cancellationToken)
    {
        var parentCollection = await dbContext.Collections.Retrieve(request.CustomerId,
            request.PresentationManifest.GetParentSlug(), cancellationToken: cancellationToken);

        var parentErrors = ValidateParent(parentCollection, request.PresentationManifest, request.UrlRoots);
        if (parentErrors != null) return parentErrors;

        var (error, dbManifest) = await UpdateDatabase(request, parentCollection!, cancellationToken);
        if (error != null) return error; 

        await SaveToS3(dbManifest!, request, cancellationToken);
        
        return PresUpdateResult.Success(
            request.PresentationManifest.SetGeneratedFields(dbManifest!, request.UrlRoots), WriteResult.Created);
    }

    private static PresUpdateResult? ValidateParent(
        Collection? parentCollection, PresentationManifest manifest, UrlRoots urlRoots)
    {
        if (parentCollection == null) return ErrorHelper.NullParentResponse<PresentationManifest>();
        if (!parentCollection.IsStorageCollection) return ManifestErrorHelper.ParentMustBeStorageCollection<PresentationManifest>();
        if (manifest.IsUriParentInvalid(parentCollection, urlRoots)) return ErrorHelper.NullParentResponse<PresentationManifest>();

        return null;
    }
    
    private async Task<(PresUpdateResult?, DbManifest?)> UpdateDatabase(
        UpsertManifestRequest request, Collection parentCollection, CancellationToken cancellationToken)
    {
        var id = await GenerateUniqueId(request, cancellationToken);
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
        var saveErrors =
            await dbContext.TrySave<PresentationManifest>("manifest", request.CustomerId, logger, cancellationToken);

        return (saveErrors, dbManifest);
    }

    private async Task<string?> GenerateUniqueId(UpsertManifestRequest request, CancellationToken cancellationToken)
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
    
    private async Task SaveToS3(DbManifest dbManifest, UpsertManifestRequest request, CancellationToken cancellationToken)
    {
        var iiifManifest = request.RawRequestBody.FromJson<IIIF.Presentation.V3.Manifest>();
        await iiifS3.SaveIIIFToS3(iiifManifest, dbManifest, dbManifest.GenerateFlatManifestId(request.UrlRoots),
            cancellationToken);
    }
}