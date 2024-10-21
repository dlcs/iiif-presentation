using System.Data;
using API.Auth;
using API.Converters;
using API.Features.Manifest.Helpers;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.AWS;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests;
using Core;
using IIIF.Serialisation;
using MediatR;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.General;
using Repository;
using Collection = Models.Database.Collections.Collection;
using DbManifest = Models.Database.Collections.Manifest;

namespace API.Features.Manifest.Requests;

/// <summary>
/// Create a new Manifest in DB and upload provided JSON to S3
/// </summary>
public class CreateManifest(
    int customerId,
    PresentationManifest presentationManifest,
    string rawRequestBody,
    UrlRoots urlRoots) : IRequest<ModifyEntityResult<PresentationManifest, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;
    public PresentationManifest PresentationManifest { get; } = presentationManifest;
    public string RawRequestBody { get; } = rawRequestBody;
    public UrlRoots UrlRoots { get; } = urlRoots;
}

public class CreateManifestHandler(
    PresentationContext dbContext,
    IIdGenerator idGenerator,
    IIIFS3Service iiifS3,
    ILogger<CreateManifestHandler> logger) : IRequestHandler<CreateManifest,
    ModifyEntityResult<PresentationManifest, ModifyCollectionType>>
{
    public async Task<ModifyEntityResult<PresentationManifest, ModifyCollectionType>> Handle(CreateManifest request,
        CancellationToken cancellationToken)
    {
        var parentCollection = await dbContext.Collections.Retrieve(request.CustomerId,
            request.PresentationManifest.GetParentSlug(), cancellationToken: cancellationToken);

        var parentErrors = ValidateParent(parentCollection);
        if (parentErrors != null) return parentErrors;

        var (error, dbManifest) = await UpdateDatabase(request, parentCollection!, cancellationToken);
        if (error != null) return error; 

        await SaveToS3(dbManifest!, request, cancellationToken);
        
        // TODO - set publicId on PresentationManifest?
        return ModifyEntityResult<PresentationManifest, ModifyCollectionType>.Success(
            request.PresentationManifest.SetGeneratedFields(dbManifest!, request.UrlRoots), WriteResult.Created);
    }

    private static ModifyEntityResult<PresentationManifest, ModifyCollectionType>? ValidateParent(Collection? parentCollection)
    {
        if (parentCollection == null) return ErrorHelper.NullParentResponse<PresentationManifest>();

        // NOTE (DG) - this is a temporary restriction
        return parentCollection.IsStorageCollection
            ? null
            : ManifestErrorHelper.ParentMustBeStorageCollection<PresentationManifest>();
    }
    
    private async Task<(ModifyEntityResult<PresentationManifest, ModifyCollectionType>?, DbManifest?)> UpdateDatabase(
        CreateManifest request, Collection parentCollection, CancellationToken cancellationToken)
    {
        var id = await GenerateUniqueId(request, cancellationToken);
        if (id == null) return (ErrorHelper.CannotGenerateUniqueId<PresentationManifest>(), null);

        // Store in DB, validating slug
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

    private async Task<string?> GenerateUniqueId(CreateManifest request, CancellationToken cancellationToken)
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
    
    private async Task SaveToS3(DbManifest dbManifest, CreateManifest request, CancellationToken cancellationToken)
    {
        var iiifManifest = request.RawRequestBody.FromJson<IIIF.Presentation.V3.Manifest>();
        await iiifS3.SaveIIIFToS3(iiifManifest, dbManifest, dbManifest.GenerateFlatManifestId(request.UrlRoots),
            cancellationToken);
    }
}