using System.Net;
using API.Attributes;
using API.Features.Manifest.Requests;
using API.Features.Manifest.Validators;
using API.Infrastructure;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using Core.IIIF;
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
    [Authorize]
    [HttpGet("manifests/{id}")]
    [ETagCaching]
    public async Task<IActionResult> GetManifestFlat([FromRoute] int customerId, [FromRoute] string id)
    {
        var manifest = await Mediator.Send(new GetManifest(customerId, id, GetUrlRoots()));
        if (manifest == null)
            return NotFound();

        if (!Request.HasShowExtraHeader())
            return manifest.FullPath is {Length: > 0} fullPath
                ? SeeOther(fullPath)
                : NotFound();

        return Ok(manifest);
    }

    [Authorize]
    [HttpPost("manifests")]
    [ETagCaching]
    public async Task<IActionResult> CreateManifest(
        [FromRoute] int customerId,
        [FromServices] PresentationManifestValidator validator,
        CancellationToken cancellationToken)
    {
        if (!Request.HasShowExtraHeader()) return this.Forbidden();

        var rawRequestBody = await Request.GetRawRequestBodyAsync(cancellationToken);
        var presentationManifest = await rawRequestBody.ToPresentation<PresentationManifest>();

        if (presentationManifest == null)
        {
            return this.PresentationProblem("Could not deserialize manifest", null, (int) HttpStatusCode.BadRequest,
                "Deserialization Error");
        }

        var validation = await validator.ValidateAsync(presentationManifest, cancellationToken);
        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        return await HandleUpsert(new CreateManifest(customerId, presentationManifest, rawRequestBody, GetUrlRoots()),
            cancellationToken: cancellationToken);
    }
}