using API.Features.Manifest.Helpers;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Requests;
using MediatR;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.Collections;
using Repository;

namespace API.Features.Manifest.Requests;

public class CreateManifest(int customerId, PresentationManifest presentationManifest) : IRequest<ModifyEntityResult<PresentationManifest, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;
    public PresentationManifest PresentationManifest { get; } = presentationManifest;
}

public class CreateManifestHandler(PresentationContext dbContext) : IRequestHandler<CreateManifest,
    ModifyEntityResult<PresentationManifest, ModifyCollectionType>>
{
    public async Task<ModifyEntityResult<PresentationManifest, ModifyCollectionType>> Handle(CreateManifest request,
        CancellationToken cancellationToken)
    {
        var parentCollection = await dbContext.Collections.Retrieve(request.CustomerId,
            request.PresentationManifest.GetParentSlug(), cancellationToken: cancellationToken);

        var invalidParent = ValidateParent(parentCollection);
        if (invalidParent != null) return invalidParent;

        // Store in DB, validating slug

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
}