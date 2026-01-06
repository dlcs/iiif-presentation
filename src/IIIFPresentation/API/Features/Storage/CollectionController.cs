using System.Net;
using API.Auth;
using API.Features.Storage.Helpers;
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
using Microsoft.Extensions.Options;

namespace API.Features.Storage;

[Route("/{customerId:int}")]
[ApiController]
public class CollectionController(
    IAuthenticator authenticator,
    IOptions<ApiSettings> options,
    IMediator mediator,
    IETagCache eTagCache,
    ILogger<CollectionController> logger)
    : PresentationController(options.Value, mediator, eTagCache, logger)
{
    [HttpGet("collections/{id}")]
    [VaryHeader]
    public async Task<IActionResult> Get(int customerId, string id, int? page = 1, int? pageSize = -1,
        string? orderBy = null, string? orderByDescending = null)
    {
        if (pageSize is null or <= 0) pageSize = Settings.PageSize;
        if (pageSize > Settings.MaxPageSize) pageSize = Settings.MaxPageSize;
        if (page is null or <= 0) page = 1;

        var orderByField = this.GetOrderBy(orderBy, orderByDescending, out var descending);

        var entityResult =
            await Mediator.Send(new GetCollection(customerId, id, Request.Headers.IfNoneMatch.AsETagValues(),
                page.Value,
                pageSize.Value, orderByField,
                descending));

        if (entityResult.ETagMatch)
            return new NotModifiedResult(entityResult.ETag!.Value);

        if (entityResult.Error)
            return this.PresentationProblem(entityResult.ErrorMessage,
                statusCode: (int)HttpStatusCode.InternalServerError);

        if (Request.HasShowExtraHeader() && await authenticator.ValidateRequest(Request) == AuthResult.Success)
        {
            return entityResult.EntityNotFound
                ? this.PresentationNotFound()
                : this.PresentationContent(entityResult.Entity!, etag: entityResult.ETag);
        }

        return entityResult.Entity?.Behavior.IsPublic() ?? false
            ? SeeOther(entityResult.Entity.PublicId!)
            : this.PresentationNotFound();
    }

    [Authorize]
    [HttpPost("collections")]
    public async Task<IActionResult> Post(int customerId)
    {
        return await HandleUpsert(new CollectionWriteRequest(customerId, HttpMethod.Post, string.Empty,
            await Request.GetRawRequestBodyAsync(), false, Request.HasShowExtraHeader(),
            Request.Headers.IfMatch));
    }

    [Authorize]
    [HttpPut("collections/{id}")]
    public async Task<IActionResult> Put(int customerId, string id)
    {
        return await HandleUpsert(new CollectionWriteRequest(customerId, HttpMethod.Put,
            id, await Request.GetRawRequestBodyAsync(), false,
            Request.HasShowExtraHeader(), Request.Headers.IfMatch
        ));
    }

    [Authorize]
    [HttpDelete("collections/{id}")]
    public async Task<IActionResult> Delete(int customerId, string id)
    {
        if (!Request.HasShowExtraHeader()) return this.Forbidden();

        return await HandleDelete(new DeleteCollection(customerId, id));
    }
}
