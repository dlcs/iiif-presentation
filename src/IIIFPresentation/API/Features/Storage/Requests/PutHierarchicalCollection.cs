using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Features.Storage.Validators;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests;
using IIIF.Presentation;
using MediatR;
using Models.API.Collection;
using Repository;
using Repository.Paths;
using DbCollection = Models.Database.Collections.Collection;

namespace API.Features.Storage.Requests;

/// <summary>
/// Create or update a Collection at the specific hierarchical path addressed by the request URL
/// (PUT /{customer}/{parent-path}/{slug}).
/// </summary>
public class PutHierarchicalCollection(
    int customerId,
    string fullPath,
    string rawRequestBody,
    string? eTag) : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    /// <summary>
    /// Full hierarchical path this request was PUT to, including the slug of the resource itself
    /// </summary>
    public string FullPath { get; } = fullPath;

    public string RawRequestBody { get; } = rawRequestBody;

    public string? ETag { get; } = eTag;
}

public class PutHierarchicalCollectionHandler(
    PresentationContext dbContext,
    ILogger<PutHierarchicalCollectionHandler> logger,
    IdentityManager identityManager,
    IRequestIdResolver requestIdResolver,
    ICollectionWrite collectionService,
    IPathGenerator pathGenerator)
    : IRequestHandler<PutHierarchicalCollection, PresentationResult>
{
    public async Task<PresentationResult> Handle(PutHierarchicalCollection request, CancellationToken cancellationToken)
    {
        var (error, context) = await HierarchicalRequestHelper.PrepareForPut<PresentationCollection, DbCollection>(
            request.RawRequestBody, request.FullPath, request.CustomerId, logger, dbContext, identityManager,
            requestIdResolver, new PresentationValidator(isFlatRequest: false),
            UpsertErrorHelper.CannotValidateIIIF(), h => h?.CollectionId, cancellationToken);
        if (error != null) return error;

        var upsertRequest = new UpsertCollectionRequest(context!.ResourceId, request.ETag, request.CustomerId,
            context.Presentation, request.RawRequestBody, urlParentPath: context.ParentPath, urlSlug: context.Slug);

        var result = await collectionService.Upsert(upsertRequest, cancellationToken);

        if (!result.IsSuccess || result.Entity is not PresentationCollection presentationCollection) return result;

        var hierarchicalId = pathGenerator.GenerateHierarchicalFromFullPath(request.CustomerId, request.FullPath);

        var responseCollection = HierarchicalCollectionResponse.Build(context.Presentation.Behavior.IsStorageCollection(),
            request.RawRequestBody, presentationCollection.Label, hierarchicalId, logger);

        return PresentationResult.Success(responseCollection, result.WriteResult, result.ETag);
    }
}
