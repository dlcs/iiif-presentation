using API.Features.Storage.Helpers;
using API.Features.Storage.Validators;
using API.Helpers;
using API.Infrastructure.Requests;
using MediatR;
using Models.API.Collection;

namespace API.Features.Storage.Requests;

/// <summary>
/// Creates a new Collection (storage or iiif) as a child of a given parent container, uploading the provided JSON
/// to S3 if it's an iiif-collection.
/// </summary>
public class CreateHierarchicalCollection(
    int customerId,
    string parentPath,
    string rawRequestBody) : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    /// <summary>
    /// Path of the parent container the new resource is created inside
    /// </summary>
    public string ParentPath { get; } = parentPath;

    public string RawRequestBody { get; } = rawRequestBody;
}

public class CreateHierarchicalCollectionHandler(
    ILogger<CreateHierarchicalCollectionHandler> logger,
    IHierarchicalRequestHelper hierarchicalRequestHelper,
    ICollectionWrite collectionService)
    : IRequestHandler<CreateHierarchicalCollection, PresentationResult>
{
    public async Task<PresentationResult> Handle(CreateHierarchicalCollection request,
        CancellationToken cancellationToken)
    {
        var (error, context) = await hierarchicalRequestHelper.PrepareForCreate<PresentationCollection>(
            request.RawRequestBody, request.ParentPath, request.CustomerId,
            new PresentationValidator(isFlatRequest: false), cancellationToken);
        if (error != null) return error;

        var writeRequest = new WriteCollectionRequest(request.CustomerId, context!.Presentation,
            request.RawRequestBody, new ResolvedLocation(context.ParentPath, context.Slug, context.ClientProvidedId));

        var result = await collectionService.Create(writeRequest, cancellationToken);

        return HierarchicalCollectionResponse.Build(result, request.RawRequestBody,
            context.Presentation.Behavior.IsStorageCollection(), logger);
    }
}
