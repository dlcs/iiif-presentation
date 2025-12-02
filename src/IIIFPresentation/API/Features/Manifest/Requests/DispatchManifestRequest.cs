using System.Diagnostics;
using API.Features.Manifest.Validators;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Features.Storage.Requests;
using API.Infrastructure.Http;
using Core;
using MediatR;
using Models.API.General;
using Models.API.Manifest;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using Result =
    API.Infrastructure.Requests.ModifyEntityResult<Models.API.Manifest.PresentationManifest,
        Models.API.General.ModifyCollectionType>;

namespace API.Features.Manifest.Requests;

/// <summary>
/// Common encapsulation of data available in a controller action, including the caller's intent
/// </summary>
/// <param name="customerId">Caller's customer id - assumed verified by authentication</param>
/// <param name="requestMethod">HTTP method used by caller, which implies intent (create at parent vs. upsert resource at location)</param>
/// <param name="requestPath">Entire path AFTER /{customerId} - might be flat, providing resource type and id, might be hierarchical showing parent and possibly the intended/desired slug</param>
/// <param name="rawRequestBody">Raw string data - will be parsed, validated etc.</param>
/// <param name="isHierarchical">Could be deduced from <paramref name="requestPath"/>, but this value is known by controller, so to simplify code it is provided explicitly</param>
/// <param name="isShowExtras">Whether the request included the "show extra properties" header</param>
///// <param name="initiatingController">Controller which created and sent this request. Used to create a response.</param>
public class DispatchManifestRequest(
    int customerId,
    HttpMethod requestMethod,
    string requestPath,
    string rawRequestBody,
    bool isHierarchical,
    bool isShowExtras,
    bool isCreateSpace,
    string? eTag
) : IRequest<Result>
{
    public int CustomerId { get; set; } = customerId;
    public HttpMethod RequestMethod { get; set; } = requestMethod;
    public string RequestPath { get; set; } = requestPath;
    public string RawRequestBody { get; set; } = rawRequestBody;
    public bool IsHierarchical { get; set; } = isHierarchical;
    public bool IsShowExtras { get; set; } = isShowExtras;
    public bool IsCreateSpace { get; set; } = isCreateSpace;
    public string? ETag { get; set; } = eTag;
}

public class DispatchManifestRequestHandler(
    PresentationContext dbContext,
    ILogger<CollectionWriteService> logger,
    IPathGenerator pathGenerator,
    IPathRewriteParser pathRewriteParser,
    PresentationManifestValidator presentationManifestValidator,
    IManifestWrite manifestService)
    : IRequestHandler<DispatchManifestRequest, Result>

{
    public async Task<Result> Handle(DispatchManifestRequest request, CancellationToken cancellationToken)
    {
        // Pre-process user-supplied data to pass to ManifestWriteService

        // 1. Non-hierarchical requests must include the extra prop header
        // source: pre-existing ManifestController.ManifestUpsert logic
        if (request is { IsHierarchical: false, IsShowExtras: false })
            return Result.Failure($"This request requires '{CustomHttpHeaders.ShowExtras}' header",
                ModifyCollectionType.ExtraHeaderRequired, WriteResult.Forbidden);

        var presentationManifest =
            await request.RawRequestBody.TryDeserializePresentation<PresentationManifest>(logger);

        if (presentationManifest.Error)
            return Result.Failure("Could not deserialize manifest", ModifyCollectionType.CannotDeserialize,
                WriteResult.BadRequest);

        // 2. Validation
        // source: existing logic in ManifestController
        var validation =
            await presentationManifestValidator.ValidateAsync(presentationManifest.ConvertedIIIF!, cancellationToken);
        if (!validation.IsValid)
        {
            var message = string.Join(". ", validation.Errors.Select(s => s.ErrorMessage).Distinct());
            return Result.Failure(message, ModifyCollectionType.ValidationFailed, WriteResult.FailedValidation);
        }

        // 3. Determine the operation
        // POST -> Create
        // PUT -> Upsert

        // An id might either be one of existing manifest, or a "desired" id, if it's provided as a valid flatId
        // A null is also acceptable - if so, we will treat it as "try creating new manifest with minted id"
        string? manifestId;

        // Note: handling of `publicId` prop is done by MWS - don't think we need to do anything with it here.

        if (request.RequestMethod == HttpMethod.Post)
        {
            // easier case - create only
            // in either hierarchical or flat case we do not have any extra knowledge about slug from path (points to parent collection only)
            // however, we do have information about a parent in hierarchical case
            if (request.IsHierarchical)
            {
                // We don't want to resolve full Hierarchy here, as it will be done as needed later
                // But, we do want to ensure there's no conflict with existing `Parent` property,
                // OR we want to add the `Parent` property that can be omitted, if it's already
                // provided via it being a hierarchical request
                var hierarchicalParentPath =
                    pathGenerator.GenerateHierarchicalFromFullPath(request.CustomerId, request.RequestPath);

                if (presentationManifest.ConvertedIIIF.Parent == null)
                {
                    // set property for use by ManifestWriteService
                    presentationManifest.ConvertedIIIF.Parent = hierarchicalParentPath;
                }
                else
                {
                    if (await CheckForParentMismatch(request, presentationManifest, hierarchicalParentPath,
                            cancellationToken) is
                        { } error)
                        return error;
                    // else no error, so proceed to calling MWS (it will use the matching value from parent property)
                }
            }

            // Before continuing we'll try to get a "manifestId" from the `id` prop
            // Note: in this scenario this is essentially just to allow passing a "desired" id in a FLAT format
            //       we pass `true` as `flatOnly` to prevent hierarhical resolution
            manifestId = await GetManifestId(request, presentationManifest.ConvertedIIIF, true, cancellationToken);

            return await CreateManifest(request, manifestId, presentationManifest.ConvertedIIIF, cancellationToken);
        }

        // else
        // PUT

        // If this is a hierarchical request, the parent and the slug might be provided via the path
        if (request.IsHierarchical)
        {
            var splitPath = request.RequestPath.Split('/');
            var pathSlug = splitPath[^1];
            var pathParent = string.Join("/", splitPath.Take(..^1));

            if (presentationManifest.ConvertedIIIF.Slug == null)
            {
                // Slug prop not provided, set from path
                presentationManifest.ConvertedIIIF.Slug = pathSlug;
            }
            else
            {
                // Slug prop provided, verify it matches path
                if (!string.Equals(presentationManifest.ConvertedIIIF.Slug, pathSlug))
                    return Result.Failure(
                        "Slug property of posted manifest does not match the slug part of hierarchical path of the request.",
                        ModifyCollectionType.SlugMustMatchPublicId, WriteResult.BadRequest);
            }

            var hierarchicalParentPath =
                pathGenerator.GenerateHierarchicalFromFullPath(request.CustomerId, pathParent);


            if (presentationManifest.ConvertedIIIF.Parent == null)
            {
                // set property for use by ManifestWriteService
                presentationManifest.ConvertedIIIF.Parent = hierarchicalParentPath;
            }
            else
            {
                if (await CheckForParentMismatch(request, presentationManifest, hierarchicalParentPath,
                        cancellationToken) is
                    { } error)
                    return error;
                // else no error, so proceed to calling MWS (it will use the matching value from parent property)
            }

            // for a hierarchical PUT <<update>>, the id has to be provided in the body.
            if (presentationManifest.ConvertedIIIF.FlatId is { Length: > 0 } flatFromBody)
            {
                // Simplest case: this is an update of existing manifest, and flatId was provided "back".
                manifestId = flatFromBody;
            }
            else
            {
                manifestId = await GetManifestId(request, presentationManifest.ConvertedIIIF, false, cancellationToken);
            }
        }
        else
        {
            // it's a flat request, so we can just grab last path segment
            manifestId =
                request.RequestPath; // in flat controller it's manifests/<id>, and <id> is passed as path, skipping the manifests part
        }

        // Finally, we're ready
        // If we got manifestId it will be an upsert request, but if not - we should be able to treat this
        // as a create request and save ourselves some effort

        return await CreateManifest(request, manifestId, presentationManifest.ConvertedIIIF, cancellationToken);
    }

    private async Task<string?> GetManifestId(DispatchManifestRequest request,
        PresentationManifest presentationManifest, bool flatOnly, CancellationToken cancellationToken)
    {
        if (TryParseProvidedId(request, presentationManifest) is
            not { resourceId: not null } pathParts) return null;

        // we got a seemingly valid id - but what we need is a flat id (or null)
        if (!pathParts.isHierarchical)
        {
            // it's flat, we're in luck
            return pathParts.resourceId;
        }

        if (flatOnly)
        {
            // for POST we don't want hierarchical resolution, it's not a supported scenario (and validation would complicate flow even more)
            return null;
        }

        // This is probably a rare scenario, so to not make it even more complicated we'll
        // resolve it with DB into a flatId
        var existingManifestHierarchy = await dbContext.RetrieveHierarchy(request.CustomerId,
            pathParts.resourceId,
            cancellationToken);

        return existingManifestHierarchy?.ManifestId; // <- if it's null, that's fine, it will be minted
    }

    private (string? resourceId, bool isHierarchical) TryParseProvidedId(DispatchManifestRequest request,
        PresentationManifest presentationManifest)
    {
        if (presentationManifest.Id is not { Length: > 0 } bodyId
            || !Uri.TryCreate(bodyId, UriKind.Absolute, out var bodyUriId))
        {
            return (null, false);
        }

        // Can be flat or hierarchical, with rewrites or not, so let's use a rewrite parser
        var pathParts = pathRewriteParser.ParsePathWithRewrites(bodyUriId.Host, bodyUriId.AbsolutePath,
            request.CustomerId);

        return pathParts.Resource is not null ? (pathParts.Resource, pathParts.Hierarchical) : (null, false);
    }

    private async Task<Result> CreateManifest(DispatchManifestRequest request,
        string? manifestId, PresentationManifest presentationManifest,
        CancellationToken cancellationToken)
    {
        if (manifestId is null)
        {
            return await WithCreateRequest(request, presentationManifest, cancellationToken);
        }

        // else upsert
        return await WithUpsertRequest(request, manifestId, presentationManifest, cancellationToken);
    }

    private async Task<Result> WithUpsertRequest(DispatchManifestRequest request,
        string manifestId, PresentationManifest presentationManifest,
        CancellationToken cancellationToken)
    {
        var upsertRequest = new UpsertManifestRequest(
            manifestId,
            request.ETag,
            request.CustomerId,
            presentationManifest,
            request.RawRequestBody,
            request.IsCreateSpace);

        return await manifestService.Upsert(upsertRequest, cancellationToken);
    }

    private async Task<Result> WithCreateRequest(DispatchManifestRequest request,
        PresentationManifest presentationManifest, CancellationToken cancellationToken)
    {
        var upsertRequest = new WriteManifestRequest(request.CustomerId,
            presentationManifest,
            request.RawRequestBody,
            request.IsCreateSpace);

        return await manifestService.Create(upsertRequest, cancellationToken);
    }

    private async Task<Result?> CheckForParentMismatch(DispatchManifestRequest request,
        TryConvertIIIFResult<PresentationManifest> presentationManifest, string pathParent,
        CancellationToken cancellationToken)
    {
        // should already have been checked
        Debug.Assert(presentationManifest.ConvertedIIIF != null, "presentationManifest.ConvertedIIIF != null");

        // we're here because the property is already set, we're gonna check if it matches posted path
        if (string.Equals(presentationManifest.ConvertedIIIF.Parent, pathParent)) return null; // no error

        // edge-case: parent might be flat and point at the same parent as the path

        // if not uri => not valid flat id => mismatch, return error
        if (!Uri.TryCreate(presentationManifest.ConvertedIIIF.Parent, UriKind.Absolute, out var parentUri))
            return Result.Failure(
                "Parent property of posted manifest does not match the hierarchical path of the request.",
                ModifyCollectionType.ParentMustMatchPublicId, WriteResult.BadRequest);

        var parentPath = pathRewriteParser.ParsePathWithRewrites(parentUri.Host, parentUri.AbsolutePath,
            request.CustomerId);

        // if is hierarchical, we would have not been here in the first place
        // also, if no flat id resolved from URL then it's obviously not valid
        if (parentPath is not { Hierarchical: false, Resource: { Length: > 0 } flatParentId })
            return Result.Failure(
                "Parent property of posted manifest does not match the hierarchical path of the request or is not a valid parent collection.",
                ModifyCollectionType.ParentMustMatchPublicId, WriteResult.BadRequest);

        // This is not great as it will be done again in MWS, but it's an edge case that we should handle here
        // but before that we have to strip the URI `pathParent` to just the AbsolutePath
        // Note: pathParent is <host>/<customer>/hierarchical/path - and as Segments includes the empty /, we skip /<customer/
        pathParent = string.Join("", new Uri(pathParent, UriKind.Absolute).Segments[2..]);

        var parentFromPathHierarchy =
            await dbContext.RetrieveHierarchy(request.CustomerId, pathParent, cancellationToken);

        // If the db check from hierarchical path returned an existing collection (so CollectionId is not null),
        // then ensure that (flat) id matches the one we got from the parent property
        if (parentFromPathHierarchy?.CollectionId is not { Length: > 0 } parentCollectionId
            || !string.Equals(parentCollectionId, flatParentId))
        {
            return Result.Failure(
                "Parent property of posted manifest does not match the hierarchical path of the request or is not a valid parent collection.",
                ModifyCollectionType.ParentMustMatchPublicId, WriteResult.BadRequest);
        }

        // the flat parent from prop matches the hierarchical parent from path, no error
        return null;
    }
}
