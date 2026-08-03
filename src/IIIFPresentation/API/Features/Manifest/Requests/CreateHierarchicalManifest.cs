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
/// Creates a new Manifest as a child of a given parent container.
/// </summary>
public class CreateHierarchicalManifest(
    int customerId,
    string parentPath,
    string rawRequestBody,
    bool createSpace) : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    /// <summary>
    /// Path of the parent container the new resource is created inside
    /// </summary>
    public string ParentPath { get; } = parentPath;

    public string RawRequestBody { get; } = rawRequestBody;

    public bool CreateSpace { get; } = createSpace;
}

public class CreateHierarchicalManifestHandler(
    IHierarchicalRequestHelper hierarchicalRequestHelper,
    IManifestWrite manifestService,
    IOptions<ServicesSettings> servicesOptions)
    : IRequestHandler<CreateHierarchicalManifest, PresentationResult>
{
    private static readonly PresentationResult DeserializeError = PresentationResult.Failure(
        "Could not deserialize manifest", ModifyCollectionType.CannotDeserialize, WriteResult.BadRequest);

    public async Task<PresentationResult> Handle(CreateHierarchicalManifest request,
        CancellationToken cancellationToken)
    {
        var (error, context) = await hierarchicalRequestHelper.PrepareForCreate<PresentationManifest>(
            request.RawRequestBody, request.ParentPath, request.CustomerId,
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
