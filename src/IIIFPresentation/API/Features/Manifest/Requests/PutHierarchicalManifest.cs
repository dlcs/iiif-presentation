using API.Converters;
using API.Features.Common.Helpers;
using API.Features.Manifest.Validators;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests;
using Core;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Models.API.General;
using Models.API.Manifest;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using Services.Manifests.Settings;
using DbManifest = Models.Database.Collections.Manifest;

namespace API.Features.Manifest.Requests;

/// <summary>
/// Create or update a Manifest at the specific hierarchical path addressed by the request URL
/// (PUT /{customer}/{parent-path}/{slug}).
/// </summary>
public class PutHierarchicalManifest(
    int customerId,
    string fullPath,
    string rawRequestBody,
    StringValues etag,
    bool createSpace) : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    /// <summary>
    /// Full hierarchical path this request was PUT to, including the slug of the resource itself
    /// </summary>
    public string FullPath { get; } = fullPath;

    public string RawRequestBody { get; } = rawRequestBody;

    public string? ETag { get; } = etag.ToString();

    public bool CreateSpace { get; } = createSpace;
}

public class PutHierarchicalManifestHandler(
    PresentationContext dbContext,
    ILogger<PutHierarchicalManifestHandler> logger,
    IdentityManager identityManager,
    IRequestIdResolver requestIdResolver,
    IManifestWrite manifestService,
    IPathGenerator pathGenerator,
    IOptions<ServicesSettings> servicesOptions)
    : IRequestHandler<PutHierarchicalManifest, PresentationResult>
{
    private static readonly PresentationResult DeserializeError = PresentationResult.Failure(
        "Could not deserialize manifest", ModifyCollectionType.CannotDeserialize, WriteResult.BadRequest);

    public async Task<PresentationResult> Handle(PutHierarchicalManifest request, CancellationToken cancellationToken)
    {
        var (error, context) = await HierarchicalRequestHelper.PrepareForPut<PresentationManifest, DbManifest>(
            request.RawRequestBody, request.FullPath, request.CustomerId, logger, dbContext, identityManager,
            requestIdResolver, new PresentationManifestValidator(servicesOptions, isFlatRequest: false),
            DeserializeError, h => h?.ManifestId, cancellationToken);
        if (error != null) return error;

        var upsertRequest = new UpsertManifestRequest(context!.ResourceId, request.ETag, request.CustomerId,
            context.Presentation, request.RawRequestBody, request.CreateSpace, urlParentPath: context.ParentPath,
            urlSlug: context.Slug);

        var result = await manifestService.Upsert(upsertRequest, cancellationToken);
        if (!result.IsSuccess || result.Entity is not PresentationManifest manifest) return result;

        var plainManifest = PresentationIIIFCleaner.OnlyIIIFProperties(manifest);
        plainManifest.Id = pathGenerator.GenerateHierarchicalFromFullPath(request.CustomerId, request.FullPath);

        return PresentationResult.Success(plainManifest, result.WriteResult, result.ETag);
    }
}
