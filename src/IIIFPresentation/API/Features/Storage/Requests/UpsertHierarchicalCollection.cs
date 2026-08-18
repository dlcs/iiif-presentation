using API.Features.Storage.Helpers;
using API.Helpers;
using API.Features.Storage.Validators;
using API.Infrastructure.Requests;
using API.Settings;
using Mediator;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using DbCollection = Models.Database.Collections.Collection;

namespace API.Features.Storage.Requests;

/// <summary>
/// Creates or updates a Collection at a specific hierarchical path.
/// </summary>
public class UpsertHierarchicalCollection(
    int customerId,
    string fullPath,
    string rawRequestBody,
    string? eTag) : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    /// <summary>
    /// Full hierarchical path of the resource, including its own slug
    /// </summary>
    public string FullPath { get; } = fullPath;

    public string RawRequestBody { get; } = rawRequestBody;

    public string? ETag { get; } = eTag;
}

public class UpsertHierarchicalCollectionHandler(
    ILogger<UpsertHierarchicalCollectionHandler> logger,
    IHierarchicalRequestHelper hierarchicalRequestHelper,
    ICollectionWrite collectionService,
    IOptions<ApiSettings> apiOptions)
    : IRequestHandler<UpsertHierarchicalCollection, PresentationResult>
{
    public async ValueTask<PresentationResult> Handle(UpsertHierarchicalCollection request,
        CancellationToken cancellationToken)
    {
        var (error, context) = await hierarchicalRequestHelper.PrepareForUpsert<PresentationCollection, DbCollection>(
            request.RawRequestBody, request.FullPath, request.CustomerId,
            new PresentationValidator(apiOptions, isFlatRequest: false), h => h?.CollectionId, cancellationToken);
        if (error != null) return error;

        var upsertRequest = new UpsertCollectionRequest(context!.ResourceId, request.ETag, request.CustomerId,
            context.Presentation, request.RawRequestBody, new ResolvedLocation(context.ParentPath, context.Slug));

        var result = await collectionService.Upsert(upsertRequest, cancellationToken);

        return HierarchicalCollectionResponse.Build(result, request.RawRequestBody,
            context.Presentation.Behavior.IsStorageCollection(), logger);
    }
}
