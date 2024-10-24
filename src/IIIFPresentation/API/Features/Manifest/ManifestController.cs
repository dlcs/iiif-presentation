using System.Net;
using API.Attributes;
using API.Features.Manifest.Requests;
using API.Features.Manifest.Validators;
using API.Infrastructure;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using Core.IIIF;
using IIIF;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Models.API.Manifest;

namespace API.Features.Manifest;

[Route("/{customerId:int}")]
[ApiController]
public class ManifestController(IOptions<ApiSettings> options, IMediator mediator)
    : PresentationController(options.Value, mediator)
{
    /// <summary>
    /// Create a new Manifest on Flat URL
    /// </summary>
    [Authorize]
    [HttpPost("manifests")]
    [ETagCaching]
    public async Task<IActionResult> CreateManifest(
        [FromRoute] int customerId,
        [FromServices] PresentationManifestValidator validator,
        CancellationToken cancellationToken) 
        => await ManifestUpsert(
            (presentationManifest, rawRequestBody) => new CreateManifest(customerId, presentationManifest, rawRequestBody, GetUrlRoots()),
            validator,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Create or upsert Manifest with specific id.
    /// If id exists valid E-Tag must be provided 
    /// </summary>
    [Authorize]
    [HttpPut("manifests/{id}")]
    [ETagCaching]
    public async Task<IActionResult> UpsertManifest(
        [FromRoute] int customerId,
        [FromRoute] string id,
        [FromServices] PresentationManifestValidator validator,
        CancellationToken cancellationToken)
        => await ManifestUpsert(
            (presentationManifest, rawRequestBody) =>
                new UpsertManifest(customerId, id, Request.Headers.IfMatch, presentationManifest, rawRequestBody, GetUrlRoots()),
            validator,
            cancellationToken: cancellationToken);
    
    private async Task<IActionResult> ManifestUpsert<T, TEnum>(
        Func<PresentationManifest, string, IRequest<ModifyEntityResult<T, TEnum>>> requestFactory,
        PresentationManifestValidator validator,
        string? instance = null,
        string? errorTitle = "Operation failed",
        CancellationToken cancellationToken = default)
        where T : class
        where TEnum : Enum
    {
        if (!Request.HasShowExtraHeader()) return this.Forbidden();

        var rawRequestBody = await Request.GetRawRequestBodyAsync(cancellationToken);
        var presentationManifest = await rawRequestBody.ToPresentation<PresentationManifest>();

        if (presentationManifest == null)
        {
            return this.PresentationProblem("Could not deserialize manifest", null, (int)HttpStatusCode.BadRequest,
                "Deserialization Error");
        }

        var validation = await validator.ValidateAsync(presentationManifest, cancellationToken);
        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        return await HandleUpsert(requestFactory(presentationManifest, rawRequestBody), instance, errorTitle,
            cancellationToken);
    }
}