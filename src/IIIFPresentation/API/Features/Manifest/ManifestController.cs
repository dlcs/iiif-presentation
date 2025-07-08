using System.Net;
using API.Auth;
using API.Features.Manifest.Requests;
using API.Features.Manifest.Validators;
using API.Features.Storage.Helpers;
using API.Infrastructure;
using API.Infrastructure.Filters;
using API.Infrastructure.Helpers;
using API.Infrastructure.Http;
using API.Infrastructure.Requests;
using API.Settings;
using IIIF;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Models.API.General;
using Models.API.Manifest;

namespace API.Features.Manifest;

[Route("/{customerId:int}")]
[ApiController]
public class ManifestController(
    IOptions<ApiSettings> options,
    IAuthenticator authenticator,
    IMediator mediator,
    IETagCache eTagCache,
    ILogger<ManifestController> logger)
    : PresentationController(options.Value, mediator, eTagCache, logger)
{
    [HttpGet("manifests/{id}")]
    [VaryHeader]
    public async Task<IActionResult> GetManifestFlat([FromRoute] int customerId, [FromRoute] string id)
    {
        var pathOnly = !Request.HasShowExtraHeader() ||
                       await authenticator.ValidateRequest(Request) != AuthResult.Success;

        var entityResult = await Mediator.Send(new GetManifest(customerId, id, Request.Headers.IfNoneMatch, pathOnly));

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
    [HttpPost("manifests")]
    public async Task<IActionResult> CreateManifest(
        [FromRoute] int customerId,
        [FromServices] PresentationManifestValidator validator,
        CancellationToken cancellationToken)
        => await ManifestUpsert(
            (presentationManifest, rawRequestBody) => new CreateManifest(customerId, presentationManifest,
                rawRequestBody, Request.HasCreateSpaceHeader()),
            validator,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Create or upsert Manifest with specific id.
    /// If id exists valid E-Tag must be provided 
    /// </summary>
    [Authorize]
    [HttpPut("manifests/{id}")]
    public async Task<IActionResult> UpsertManifest(
        [FromRoute] int customerId,
        [FromRoute] string id,
        [FromServices] PresentationManifestValidator validator,
        CancellationToken cancellationToken)
        => await ManifestUpsert(
            (presentationManifest, rawRequestBody) =>
                new UpsertManifest(customerId, id, Request.Headers.IfMatch, presentationManifest, rawRequestBody,
                    Request.HasCreateSpaceHeader()),
            validator,
            invalidatesEtag:Request.Headers.IfMatch,
            cancellationToken: cancellationToken);

    [Authorize]
    [HttpDelete("manifests/{id}")]
    public async Task<IActionResult> Delete(int customerId, string id)
    {
        if (!Request.HasShowExtraHeader()) return this.Forbidden();

        return await HandleDelete(new DeleteManifest(customerId, id));
    }

    private async Task<IActionResult> ManifestUpsert<T, TEnum>(
        Func<PresentationManifest, string, IRequest<ModifyEntityResult<T, TEnum>>> requestFactory,
        PresentationManifestValidator validator,
        string? instance = null,
        string? errorTitle = "Operation failed",
        string? invalidatesEtag = null,
        CancellationToken cancellationToken = default)
        where T : JsonLdBase
        where TEnum : Enum
    {
        if (!Request.HasShowExtraHeader()) return this.Forbidden();

        var rawRequestBody = await Request.GetRawRequestBodyAsync(cancellationToken);
        var presentationManifest = await rawRequestBody.TryDeserializePresentation<PresentationManifest>(logger);

        if (presentationManifest.Error)
        {
            return this.PresentationProblem("Could not deserialize manifest", null, (int)HttpStatusCode.BadRequest,
                "Deserialization Error", this.GetErrorType(ModifyCollectionType.CannotDeserialize));
        }

        var validation = await validator.ValidateAsync(presentationManifest.ConvertedIIIF!, cancellationToken);
        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        return await HandleUpsert(requestFactory(presentationManifest.ConvertedIIIF!, rawRequestBody), instance,
            errorTitle, invalidatesEtag, cancellationToken);
    }
}
