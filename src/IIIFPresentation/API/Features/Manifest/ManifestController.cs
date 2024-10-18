using System.Net;
using API.Attributes;
using API.Infrastructure;
using API.Infrastructure.Helpers;
using API.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Manifest;

[Route("/{customerId:int}")]
[ApiController]
public class ManifestController(IOptions<ApiSettings> options, IMediator mediator)
    : PresentationController(options.Value, mediator)
{
    [Authorize]
    [HttpPost("manifests")]
    [ETagCaching]
    public async Task<IActionResult> CreateManifest([FromRoute] int customerId)
    {
        if (!Request.HasShowExtraHeader()) return this.Forbidden();

        throw new NotImplementedException();
    }
}