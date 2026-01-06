using System.Net;
using API.Auth;
using API.Features.Manifest.Requests;
using API.Features.Manifest.Validators;
using API.Infrastructure;
using API.Infrastructure.Filters;
using API.Infrastructure.Helpers;
using API.Infrastructure.Http;
using API.Infrastructure.Requests;
using API.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Manifest;

[Route("/{customerId:int}/manifests")]
[ApiController]
public class ManifestController(
    IOptions<ApiSettings> options,
    IAuthenticator authenticator,
    IMediator mediator,
    IETagCache eTagCache,
    ILogger<ManifestController> logger)
    : PresentationController(options.Value, mediator, eTagCache, logger)
{
    [HttpGet("{id}")]
    [VaryHeader]
    public async Task<IActionResult> GetManifestFlat([FromRoute] int customerId, [FromRoute] string id)
    {
        var pathOnly = !Request.HasShowExtraHeader() ||
                       await authenticator.ValidateRequest(Request) != AuthResult.Success;

        var entityResult =
            await Mediator.Send(new GetManifest(customerId, id, Request.Headers.IfNoneMatch.AsETagValues(), pathOnly));

        switch (entityResult)
        {
            case { EntityNotFound: true }: return this.PresentationNotFound();
            case { Error: true }:
                return this.PresentationProblem(entityResult.ErrorMessage,
                    statusCode: (int)HttpStatusCode.InternalServerError);
            case { ETagMatch: true, ETag: { } etag }: return new NotModifiedResult(etag);
            case { Entity: not null }: break;

            default: return this.PresentationNotFound();
        }


        if (pathOnly) // only .FullPath is actually filled, this is to avoid S3 read
            return entityResult.Entity.FullPath is { Length: > 0 } fullPath
                ? SeeOther(fullPath)
                : this.PresentationNotFound();

        var statusCode = entityResult.Entity.CurrentlyIngesting ? HttpStatusCode.Accepted : HttpStatusCode.OK;
        return this.PresentationContent(entityResult.Entity, (int)statusCode, entityResult.ETag);
    }

    /// <summary>
    /// Create a new Manifest on Flat URL
    /// </summary>
    [Authorize]
    [HttpPost("")]
    public async Task<IActionResult> CreateManifest(
        [FromRoute] int customerId,
        [FromServices] PresentationManifestValidator validator,
        CancellationToken cancellationToken)
        => await HandleUpsert(new DispatchManifestRequest(customerId, HttpMethod.Post, string.Empty,
                await Request.GetRawRequestBodyAsync(cancellationToken),
                false, Request.HasShowExtraHeader(), Request.HasCreateSpaceHeader(), Request.Headers.IfMatch),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Create or upsert Manifest with specific id.
    /// If id exists valid E-Tag must be provided 
    /// </summary>
    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpsertManifest(
        [FromRoute] int customerId,
        [FromRoute] string id,
        [FromServices] PresentationManifestValidator validator,
        CancellationToken cancellationToken)
        => await HandleUpsert(
            new DispatchManifestRequest(customerId, HttpMethod.Put, id,
                await Request.GetRawRequestBodyAsync(cancellationToken),
                false, Request.HasShowExtraHeader(), Request.HasCreateSpaceHeader(), Request.Headers.IfMatch),
            invalidatesEtag: Request.Headers.IfMatch,
            cancellationToken: cancellationToken);

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int customerId, string id)
    {
        if (!Request.HasShowExtraHeader()) return this.Forbidden();

        return await HandleDelete(new DeleteManifest(customerId, id));
    }
}
