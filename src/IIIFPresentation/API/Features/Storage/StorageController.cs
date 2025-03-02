using System.Net;
using API.Attributes;
using API.Auth;
using API.Converters;
using API.Features.Manifest.Requests;
using API.Features.Storage.Requests;
using API.Infrastructure;
using API.Infrastructure.Filters;
using API.Infrastructure.Helpers;
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

[Route("/{customerId:int}")]
[ApiController]
public class StorageController(
    IAuthenticator authenticator,
    PresentationContext dbContext,
    IOptions<ApiSettings> options,
    IPathGenerator pathGenerator,
    IMediator mediator,
    ILogger<StorageController> logger)
    : PresentationController(options.Value, mediator, logger)
{
    [HttpGet("{*slug}")]
    [ETagCaching]
    [VaryHeader]
    public async Task<IActionResult> GetHierarchical(int customerId, string slug = "")
    {
        var hierarchy = await dbContext.RetrieveHierarchy(customerId, slug);
        
        if (hierarchy == null) return this.PresentationNotFound();
        hierarchy.FullPath = slug;

        switch (hierarchy.Type)
        {
            case ResourceType.IIIFManifest:
                if (Request.HasShowExtraHeader() && await authenticator.ValidateRequest(Request) == AuthResult.Success)
                    return hierarchy.ManifestId == null
                        ? this.PresentationNotFound()
                        : SeeOther($"manifests/{hierarchy.ManifestId}");
                
                var storedManifest = await mediator.Send(new GetManifestHierarchical(hierarchy));
                return storedManifest == null ? this.PresentationNotFound() : this.PresentationContent(storedManifest);

            case ResourceType.IIIFCollection:
            case ResourceType.StorageCollection:
                var storageRoot = await Mediator.Send(new GetHierarchicalCollection(hierarchy));

                if (storageRoot.Collection == null) return this.PresentationNotFound();

                if (Request.HasShowExtraHeader() && await authenticator.ValidateRequest(Request) == AuthResult.Success)
                {
                    var relativeUrl = pathGenerator.GenerateFlatCollectionId(storageRoot.Collection);
                    relativeUrl = QueryHelpers.AddQueryString(relativeUrl, Request.Query);
                    return SeeOther(relativeUrl);
                }

                return storageRoot.StoredCollection == null
                    ? this.PresentationContent(storageRoot.Collection.ToHierarchicalCollection(pathGenerator, storageRoot.Items))
                    : this.PresentationContent(storageRoot.StoredCollection);

            default:
                return this.PresentationProblem("Cannot fulfill this resource type", null,
                    (int)HttpStatusCode.InternalServerError, "Cannot fulfill this resource type");
        }
    }

    [Authorize]
    [HttpPost("{*slug}")]
    [ETagCaching]
    public async Task<IActionResult> PostHierarchicalCollection(int customerId, string slug)
    {
        // X-IIIF-CS-Show-Extras is not required here, the body should be vanilla json
        var rawRequestBody = await Request.GetRawRequestBodyAsync();
        return await HandleUpsert(new PostHierarchicalCollection(customerId, slug, rawRequestBody));
    }
}
