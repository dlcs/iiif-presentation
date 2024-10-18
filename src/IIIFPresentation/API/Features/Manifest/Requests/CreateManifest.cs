using System.Data;
using API.Auth;
using API.Features.Manifest.Helpers;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests;
using MediatR;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using DbManifest = Models.Database.Collections.Manifest;

namespace API.Features.Manifest.Requests;

public class CreateManifest(int customerId, PresentationManifest presentationManifest) : IRequest<ModifyEntityResult<PresentationManifest, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;
    public PresentationManifest PresentationManifest { get; } = presentationManifest;
}

public class CreateManifestHandler(
    PresentationContext dbContext,
    ILogger<CreateManifestHandler> logger,
    IIdGenerator idGenerator) : IRequestHandler<CreateManifest,
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

        // Store in S3

        throw new NotImplementedException();
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
                    Type = ResourceType.Manifest,
                    Parent = parentCollection!.Id,
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
}