using System.Net;
using API.Attributes;
using API.Auth;
using API.Converters;
using API.Features.Manifest.Requests;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Features.Storage.Requests;
using API.Features.Storage.Validators;
using API.Helpers;
using API.Infrastructure;
using API.Infrastructure.Filters;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using IIIF.Presentation;
using IIIF.Serialisation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.Database.General;
using Repository;
using Repository.Helpers;

namespace API.Features.Storage;

[Route("/{customerId:int}")]
[ApiController]
public class StorageController(
    IAuthenticator authenticator,
    PresentationContext dbContext,
    IOptions<ApiSettings> options,
    IMediator mediator)
    : PresentationController(options.Value, mediator)
{
    [HttpGet("{*slug}")]
    [ETagCaching]
    [VaryHeader]
    public async Task<IActionResult> GetHierarchical(int customerId, string slug = "")
    {
        var hierarchy =
            await dbContext.RetrieveHierarchy(customerId, slug);

        switch (hierarchy?.Type)
        {
            case ResourceType.IIIFManifest:
                if (Request.HasShowExtraHeader())
                    return hierarchy.ManifestId == null ? NotFound() : SeeOther($"manifests/{hierarchy.ManifestId}");

                var storedManifest = await mediator.Send(new GetManifestHierarchical(hierarchy, slug, GetUrlRoots()));
                return storedManifest == null ? NotFound() : Content(storedManifest, ContentTypes.V3);

            case ResourceType.IIIFCollection:
            case ResourceType.StorageCollection:
                var storageRoot = await Mediator.Send(new GetHierarchicalCollection(hierarchy, slug, GetUrlRoots()));

                if (storageRoot.Collection is not {IsPublic: true}) return this.PresentationNotFound();

                if (Request.HasShowExtraHeader() && await authenticator.ValidateRequest(Request) == AuthResult.Success)
                {
                    var relativeUrl = storageRoot.Collection.GenerateFlatCollectionId(GetUrlRoots());
                    relativeUrl = QueryHelpers.AddQueryString(relativeUrl, Request.Query);
                    return SeeOther(relativeUrl);
                }

                return storageRoot.StoredCollection == null
                    ? Content(
                        storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items).AsJson(),
                        ContentTypes.V3)
                    : Content(storageRoot.StoredCollection, ContentTypes.V3);

            default:
                return this.PresentationNotFound();
        }
    }

    [Authorize]
    [HttpPost("{*slug}")]
    [ETagCaching]
    public async Task<IActionResult> PostHierarchicalCollection(int customerId, string slug)
    {
        // X-IIIF-CS-Show-Extras is not required here, the body should be vanilla json
        var rawRequestBody = await Request.GetRawRequestBodyAsync();
        return await HandleUpsert(new PostHierarchicalCollection(customerId, slug, GetUrlRoots(), rawRequestBody));
    }
}