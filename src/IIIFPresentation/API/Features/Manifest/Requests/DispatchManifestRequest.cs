using API.Features.Manifest.Validators;
using API.Features.Storage.Helpers;
using API.Features.Storage.Requests;
using API.Features.Storage.Validators;
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

        if (request.RequestMethod == HttpMethod.Post)
        {
            // easier case - create only
            // in either hierarchical or flat case we do not have any extra knowledge about slug or id
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
                    // property already set, check if it matches posted path
                    if (!string.Equals(presentationManifest.ConvertedIIIF.Parent, hierarchicalParentPath))
                        return Result.Failure(
                            "Parent property of posted manifest does not match the hierarchical path of the request.",
                            ModifyCollectionType.ParentMustMatchPublicId, WriteResult.BadRequest);
                }
            }

            var upsertRequest = new WriteManifestRequest(request.CustomerId,
                presentationManifest.ConvertedIIIF,
                request.RawRequestBody,
                request.IsCreateSpace);

            return await manifestService.Create(upsertRequest, cancellationToken);
        }
        else
        {
            // PUT
            // We will need manifestId later, if one was provided, to facilitate <<update>> scenario
            string? manifestId = null;

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

                if (presentationManifest.ConvertedIIIF.Parent == null)
                {
                    // set property for use by ManifestWriteService
                    presentationManifest.ConvertedIIIF.Parent = pathParent;
                }
                else
                {
                    // property already set, check if it matches posted path
                    if (!string.Equals(presentationManifest.ConvertedIIIF.Parent, pathParent))
                        return Result.Failure(
                            "Parent property of posted manifest does not match the hierarchical path of the request.",
                            ModifyCollectionType.ParentMustMatchPublicId, WriteResult.BadRequest);
                }

                // for a hierarchical PUT <<update>>, the id has to be provided in the body.
                if (presentationManifest.ConvertedIIIF.FlatId is { Length: > 0 } flatFromBody)
                {
                    // Simplest case: this is an update of existing manifest, and flatId was provided "back".
                    manifestId = flatFromBody;
                }
                else if (presentationManifest.ConvertedIIIF.Id is { Length: > 0 } bodyId
                         && Uri.TryCreate(bodyId, UriKind.Absolute, out var bodyUriId))
                {
                    // Can be flat or hierarchical, with rewrites or not, so let's use a rewrite parser
                    var pathParts = pathRewriteParser.ParsePathWithRewrites(bodyUriId.Host, bodyUriId.AbsolutePath,
                        request.CustomerId);

                    if (pathParts.Resource is not null)
                    {
                        // we got a seemingly valid id - but what we need is a flat id (or null if this is NOT update)
                        if (pathParts.Hierarchical)
                        {
                            // This is probably a rare scenario, so to not make it even more complicated we'll
                            // resolve it with DB into a flatId
                            var existingManifestHierarchy = await dbContext.RetrieveHierarchy(request.CustomerId,
                                pathParts.Resource,
                                cancellationToken);
                            manifestId =
                                existingManifestHierarchy
                                    ?.ManifestId; // <- if it's null, that's fine, we'll handlke it below
                        }
                        else
                        {
                            // it's flat, we're in luck
                            manifestId = pathParts.Resource;
                        }
                    }
                }
            }
            else
            {
                // it's a flat request, so we can just grab last path segment
                manifestId = request.RequestPath.Split('/')[^1];
            }

            // Finally, we're ready
            // If we got manifestId it will be an upsert request, but if not - we should be able to treat this
            // as a create request and save ourselves some effort

            if (manifestId is null)
            {
                var upsertRequest = new WriteManifestRequest(request.CustomerId,
                    presentationManifest.ConvertedIIIF,
                    request.RawRequestBody,
                    request.IsCreateSpace);

                return await manifestService.Create(upsertRequest, cancellationToken);
            }
            else
            {
                var upsertRequest = new UpsertManifestRequest(
                    manifestId,
                    request.ETag,
                    request.CustomerId,
                    presentationManifest.ConvertedIIIF,
                    request.RawRequestBody,
                    request.IsCreateSpace);

                return await manifestService.Upsert(upsertRequest, cancellationToken);
            }
        }
    }
}
