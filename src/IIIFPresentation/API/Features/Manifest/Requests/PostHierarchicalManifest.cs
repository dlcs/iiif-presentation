using API.Features.Manifest.Helpers;
using API.Features.Manifest.Validators;
using API.Helpers;
using API.Infrastructure.Requests;
using Core;
using MediatR;
using Microsoft.Extensions.Options;
using Models.API.General;
using Models.API.Manifest;
using Services.Manifests.Settings;

namespace API.Features.Manifest.Requests;

/// <summary>
/// Create a new Manifest as a child of the container addressed by the request URL
/// (POST /{customer}/{parent-path}).
/// </summary>
public class PostHierarchicalManifest(
    int customerId,
    string parentPath,
    string rawRequestBody,
    bool createSpace) : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    /// <summary>
    /// Full hierarchical path this request was POSTed to - the parent container for the new resource
    /// </summary>
    public string ParentPath { get; } = parentPath;

    public string RawRequestBody { get; } = rawRequestBody;

    public bool CreateSpace { get; } = createSpace;
}

public class PostHierarchicalManifestHandler(
    ILogger<PostHierarchicalManifestHandler> logger,
    IRequestIdResolver requestIdResolver,
    IManifestWrite manifestService,
    IOptions<ServicesSettings> servicesOptions)
    : IRequestHandler<PostHierarchicalManifest, PresentationResult>
{
    private static readonly PresentationResult DeserializeError = PresentationResult.Failure(
        "Could not deserialize manifest", ModifyCollectionType.CannotDeserialize, WriteResult.BadRequest);

    public async Task<PresentationResult> Handle(PostHierarchicalManifest request, CancellationToken cancellationToken)
    {
        var (error, context) = await HierarchicalRequestHelper.PrepareForPost<PresentationManifest>(
            request.RawRequestBody, request.ParentPath, request.CustomerId, logger, requestIdResolver,
            new PresentationManifestValidator(servicesOptions, isFlatRequest: false), DeserializeError,
            cancellationToken);
        if (error != null) return error;

        var writeRequest = new WriteManifestRequest(request.CustomerId, context!.Presentation,
            request.RawRequestBody, request.CreateSpace, urlParentPath: context.ParentPath, urlSlug: context.Slug,
            clientProvidedId: context.ClientProvidedId);

        var result = await manifestService.Create(writeRequest, cancellationToken);

        return HierarchicalManifestResponse.Build(result);
    }
}
