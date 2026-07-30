using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Features.Storage.Validators;
using API.Helpers;
using API.Infrastructure.Requests;
using Core.Helpers;
using IIIF.Presentation;
using MediatR;
using Models.API.Collection;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using DbCollection = Models.Database.Collections.Collection;

namespace API.Features.Storage.Requests;

/// <summary>
/// Create a new Collection (storage or iiif) as a child of the container addressed by the request URL
/// (POST /{customer}/{parent-path}), and upload provided JSON to S3 if iiif-collection.
/// </summary>
public class PostHierarchicalCollection(
    int customerId,
    string parentPath,
    string rawRequestBody) : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    /// <summary>
    /// Full hierarchical path this request was POSTed to - the parent container for the new resource
    /// </summary>
    public string ParentPath { get; } = parentPath;

    public string RawRequestBody { get; } = rawRequestBody;
}

public class PostHierarchicalCollectionHandler(
    ILogger<PostHierarchicalCollectionHandler> logger,
    IRequestIdResolver requestIdResolver,
    ICollectionWrite collectionService,
    PresentationContext dbContext,
    IPathGenerator pathGenerator)
    : IRequestHandler<PostHierarchicalCollection, PresentationResult>
{
    public async Task<PresentationResult> Handle(PostHierarchicalCollection request,
        CancellationToken cancellationToken)
    {
        var (error, context) = await HierarchicalRequestHelper.PrepareForPost<PresentationCollection>(
            request.RawRequestBody, request.ParentPath, request.CustomerId, logger, requestIdResolver,
            new PresentationValidator(isFlatRequest: false), UpsertErrorHelper.CannotValidateIIIF(),
            cancellationToken);
        if (error != null) return error;

        var writeRequest = new WriteCollectionRequest(request.CustomerId, context!.Presentation,
            request.RawRequestBody, urlParentPath: context.ParentPath, urlSlug: context.Slug,
            clientProvidedId: context.ClientProvidedId);

        var result = await collectionService.Create(writeRequest, cancellationToken);
        if (!result.IsSuccess || result.Entity is not PresentationCollection collection) return result;

        var dbCollection = new DbCollection
        {
            Id = collection.FlatId.ThrowIfNull(nameof(collection.FlatId)), CustomerId = request.CustomerId
        };
        var fullPath =
            await CollectionRetrieval.RetrieveFullPathForCollection(dbCollection, dbContext, cancellationToken);
        var hierarchicalId = pathGenerator.GenerateHierarchicalFromFullPath(request.CustomerId, fullPath);

        return HierarchicalCollectionResponse.Build(result, request.RawRequestBody,
            context.Presentation.Behavior.IsStorageCollection(), hierarchicalId, logger);
    }
}
