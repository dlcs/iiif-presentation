using API.Features.Manifest.Helpers;
using API.Features.Manifest.Validators;
using API.Helpers;
using API.Infrastructure.Requests;
using Core;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Models.API.General;
using Models.API.Manifest;
using Services.Manifests.Settings;
using DbManifest = Models.Database.Collections.Manifest;

namespace API.Features.Manifest.Requests;

/// <summary>
/// Creates or updates a Manifest at a specific hierarchical path.
/// </summary>
public class UpsertHierarchicalManifest(
    int customerId,
    string fullPath,
    string rawRequestBody,
    StringValues etag,
    bool createSpace) : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    /// <summary>
    /// Full hierarchical path of the resource, including its own slug
    /// </summary>
    public string FullPath { get; } = fullPath;

    public string RawRequestBody { get; } = rawRequestBody;

    public string? ETag { get; } = etag.ToString();

    public bool CreateSpace { get; } = createSpace;
}

public class UpsertHierarchicalManifestHandler(
    IHierarchicalRequestHelper hierarchicalRequestHelper,
    IManifestWrite manifestService,
    IOptions<ServicesSettings> servicesOptions)
    : IRequestHandler<UpsertHierarchicalManifest, PresentationResult>
{
    private static readonly PresentationResult DeserializeError = PresentationResult.Failure(
        "Could not deserialize manifest", ModifyCollectionType.CannotDeserialize, WriteResult.BadRequest);

    public async Task<PresentationResult> Handle(UpsertHierarchicalManifest request,
        CancellationToken cancellationToken)
    {
        var (error, context) = await hierarchicalRequestHelper.PrepareForUpsert<PresentationManifest, DbManifest>(
            request.RawRequestBody, request.FullPath, request.CustomerId,
            new PresentationManifestValidator(servicesOptions, isFlatRequest: false), DeserializeError,
            h => h?.ManifestId, cancellationToken);
        if (error != null) return error;

        var upsertRequest = new UpsertManifestRequest(context!.ResourceId, request.ETag, request.CustomerId,
            context.Presentation, request.RawRequestBody, request.CreateSpace,
            new ResolvedLocation(context.ParentPath, context.Slug));

        var result = await manifestService.Upsert(upsertRequest, cancellationToken);

        return HierarchicalManifestResponse.Build(result);
    }
}
