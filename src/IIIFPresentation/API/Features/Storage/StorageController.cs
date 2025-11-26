using System.Diagnostics;
using System.Net;
using API.Auth;
using API.Converters;
using API.Features.Manifest.Requests;
using API.Features.Storage.Requests;
using API.Infrastructure;
using API.Infrastructure.Filters;
using API.Infrastructure.Helpers;
using API.Infrastructure.Http;
using API.Infrastructure.Requests;
using API.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Repository.Paths;

namespace API.Features.Storage;

[Route("/{customerId:int}/{*slug}")]
[ApiController]
public class StorageController(
    IAuthenticator authenticator,
    PresentationContext dbContext,
    IETagCache eTagCache,
    IOptions<ApiSettings> options,
    IPathGenerator pathGenerator,
    IMediator mediator,
    ILogger<StorageController> logger)
    : PresentationController(options.Value, mediator, eTagCache, logger)
{
    [HttpGet("")]
    [VaryHeader]
    public async Task<IActionResult> GetHierarchical(int customerId, string slug = "")
    {
        var ifNoneMatch = Request.Headers.IfNoneMatch.AsETagValues();
        if (EtagCache.IfNoneMatchForPath(slug, ifNoneMatch, out var matchedTag))
            return new NotModifiedResult(matchedTag.Value);

        var hierarchy = await dbContext.RetrieveHierarchy(customerId, slug);

        if (hierarchy == null) return this.PresentationNotFound();
        hierarchy.FullPath = slug;

        switch (hierarchy.Type)
        {
            case ResourceType.IIIFManifest:
                if (Request.HasShowExtraHeader() && await authenticator.ValidateRequest(Request) == AuthResult.Success)
                {
                    return hierarchy.ManifestId == null
                        ? this.PresentationNotFound()
                        : SeeOther(pathGenerator.GenerateFlatId(hierarchy));
                }

                var storedManifest = await Mediator.Send(new GetManifestHierarchical(hierarchy));
                if (storedManifest == null)
                    return this.PresentationNotFound();

                var mEtag = hierarchy.Manifest?.Etag;
                if (mEtag.HasValue)
                    EtagCache.SetEtagForPath(slug, mEtag.Value);

                return this.PresentationContent(storedManifest, etag: mEtag);

            case ResourceType.IIIFCollection:
            case ResourceType.StorageCollection:
                var storageRoot = await Mediator.Send(new GetHierarchicalCollection(hierarchy));

                if (storageRoot.Collection == null) return this.PresentationNotFound();

                if (Request.HasShowExtraHeader() && await authenticator.ValidateRequest(Request) == AuthResult.Success)
                {
                    var absoluteUri = pathGenerator.GenerateFlatId(hierarchy);
                    absoluteUri = QueryHelpers.AddQueryString(absoluteUri, Request.Query);
                    return SeeOther(absoluteUri);
                }

                var cEtag = hierarchy.Collection?.Etag;
                if (cEtag.HasValue)
                    EtagCache.SetEtagForPath(slug, cEtag.Value);

                return storageRoot.StoredCollection == null
                    ? this.PresentationContent(
                        storageRoot.Collection.ToHierarchicalCollection(pathGenerator, storageRoot.Items), etag: cEtag)
                    : this.PresentationContent(storageRoot.StoredCollection, etag: cEtag);

            default:
                return this.PresentationProblem("Cannot fulfill this resource type", null,
                    (int)HttpStatusCode.InternalServerError, "Cannot fulfill this resource type");
        }
    }

    [Authorize]
    [HttpPost("")]
    public async Task<IActionResult> PostHierarchical(int customerId, string? slug)
    {
        slug ??= string.Empty;
        
        // X-IIIF-CS-Show-Extras is not required here, the body should be vanilla json
        var rawRequestBody = await Request.GetRawRequestBodyAsync();
        
        // This will load string value of `type` property on the top level of the JSON, if present
        var type = FastJsonPropertyRead.FindAtLevel(rawRequestBody, "type");
        
        return type switch
        {
            nameof(IIIF.Presentation.V3.Manifest) => await HandleUpsert(
                new PostHierarchicalManifest(customerId, slug, rawRequestBody)),
            nameof(IIIF.Presentation.V3.Collection) => await HandleUpsert(
                new CollectionWriteRequest(customerId, HttpMethod.Post, slug, rawRequestBody,
                    true, Request.HasShowExtraHeader(), Request.Headers.IfMatch)),
            _ => this.PresentationProblem("Unsupported resource type", statusCode: 400)
        };
    }
    
    [Authorize]
    [HttpPut("")]
    public async Task<IActionResult> PutHierarchical(int customerId, string slug)
    {
        var rawRequestBody = await Request.GetRawRequestBodyAsync();
        
        // This will load string value of `type` property on the top level of the JSON, if present
        var type = FastJsonPropertyRead.FindAtLevel(rawRequestBody, "type");
        
        return type switch
        {
            nameof(IIIF.Presentation.V3.Manifest) => await HandleUpsert(
                new PutHierarchicalManifest(customerId, slug, rawRequestBody)),
            nameof(IIIF.Presentation.V3.Collection) => await HandleUpsert(
                new CollectionWriteRequest(customerId, HttpMethod.Put,  slug, rawRequestBody,
                    true, Request.HasShowExtraHeader(), Request.Headers.IfMatch)),
            _ => this.PresentationProblem("Unsupported resource type", statusCode: 400)
        };
    }

    [Authorize]
    [HttpDelete("")]
    public async Task<IActionResult> DeleteHierarchical(int customerId, string slug)
    {
        var hierarchy = await dbContext.RetrieveHierarchy(customerId, slug);
        if (hierarchy == null) return this.PresentationNotFound();
        
        switch (hierarchy.Type)
        {
            case ResourceType.IIIFManifest:
                Debug.Assert(hierarchy.ManifestId != null, "hierarchy.ManifestId != null");
                return await HandleDelete(new DeleteManifest(customerId, hierarchy.ManifestId));
            
            case ResourceType.IIIFCollection:
            case ResourceType.StorageCollection:
                Debug.Assert(hierarchy.CollectionId != null, "hierarchy.CollectionId != null");
                return await HandleDelete(new DeleteCollection(customerId, hierarchy.CollectionId));
            
            default:
                return this.PresentationProblem("Cannot fulfill this resource type", null,
                    (int)HttpStatusCode.InternalServerError, "Cannot fulfill this resource type");
        }
    }
}
