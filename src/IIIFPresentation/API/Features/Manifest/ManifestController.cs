using System.Net;
using API.Attributes;
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
    [HttpPost("manifests")]
    [ETagCaching]
    public async Task<IActionResult> CreateManifest(
        [FromRoute] int customerId,
        [FromServices] PresentationManifestValidator validator,
        CancellationToken cancellationToken)
    {
        if (!Request.HasShowExtraHeader()) return this.Forbidden();

        var rawRequestBody = await Request.GetRawRequestBodyAsync(cancellationToken);
        var manifest = await rawRequestBody.ToPresentation<PresentationManifest>();

        if (manifest == null)
        {
            return this.PresentationProblem("Could not deserialize manifest", null, (int)HttpStatusCode.BadRequest,
                "Deserialization Error");
        }

        var validation = await validator.ValidateAsync(manifest, cancellationToken);
        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }
        
        throw new NotImplementedException();
    }
}