using System.Net;
using API.Attributes;
using API.Auth;
using API.Converters;
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
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Models.API.Collection;

namespace API.Features.Storage;

[Route("/{customerId:int}")]
[ApiController]
public class CollectionController(
    IAuthenticator authenticator,
    IOptions<ApiSettings> options,
    IMediator mediator,
    IPathGenerator pathGenerator)
    : PresentationController(options.Value, mediator)
{
    [HttpGet("collections/{id}")]
    [ETagCaching]
    [VaryHeader]
    public async Task<IActionResult> Get(int customerId, string id, int? page = 1, int? pageSize = -1,
        string? orderBy = null, string? orderByDescending = null)
    {
        if (pageSize is null or <= 0) pageSize = Settings.PageSize;
        if (pageSize > Settings.MaxPageSize) pageSize = Settings.MaxPageSize;
        if (page is null or <= 0) page = 1;

        var orderByField = this.GetOrderBy(orderBy, orderByDescending, out var descending);
        var storageRoot =
            await Mediator.Send(new GetCollection(customerId, id, page.Value, pageSize.Value, orderByField,
                descending));

        if (storageRoot.Collection == null) return this.PresentationNotFound();

        if (Request.HasShowExtraHeader() && await authenticator.ValidateRequest(Request) == AuthResult.Success)
        {
            var orderByParameter = orderByField != null
                ? $"{(descending ? nameof(orderByDescending) : nameof(orderBy))}={orderByField}"
                : null;

            return Ok(storageRoot.Collection.ToFlatCollection( pageSize.Value, page.Value,
                storageRoot.TotalItems, storageRoot.Items, pathGenerator, orderByParameter));
        }

        return storageRoot.Collection.IsPublic
            ? SeeOther( pathGenerator.GenerateHierarchicalCollectionId(storageRoot.Collection))
            : this.PresentationNotFound();
    }

    [Authorize]
    [HttpPost("collections")]
    [ETagCaching]
    public async Task<IActionResult> Post(int customerId, [FromServices] PresentationCollectionValidator validator)
    {
        var deserializeValidationResult = await DeserializeAndValidate(validator);
        if (deserializeValidationResult.Error != null) return deserializeValidationResult.Error;

        return await HandleUpsert(new CreateCollection(customerId,
            deserializeValidationResult.ConvertedIIIF, deserializeValidationResult.RawRequestBody));
    }

    [Authorize]
    [HttpPut("collections/{id}")]
    [ETagCaching]
    public async Task<IActionResult> Put(int customerId, string id,
        [FromServices] PresentationCollectionValidator validator)
    {
        var deserializeValidationResult = await DeserializeAndValidate(validator);
        if (deserializeValidationResult.Error != null) return deserializeValidationResult.Error;

        return await HandleUpsert(new UpsertCollection(customerId, id,
            deserializeValidationResult.ConvertedIIIF, Request.Headers.IfMatch,
            deserializeValidationResult.RawRequestBody));
    }


    private async Task<DeserializeValidationResult<PresentationCollection>> DeserializeAndValidate(
        PresentationCollectionValidator validator)
    {
        if (!Request.HasShowExtraHeader())
            return DeserializeValidationResult<PresentationCollection>.Failure(this.Forbidden());

        var rawRequestBody = await Request.GetRawRequestBodyAsync();

        var deserializedCollection = await rawRequestBody.TryDeserializePresentationCollection();
        if (deserializedCollection.Error)
        {
            return DeserializeValidationResult<PresentationCollection>.Failure(PresentationUnableToSerialize());
        }

        var validation = await validator.ValidateAsync(deserializedCollection.ConvertedIIIF!);

        if (!validation.IsValid)
        {
            return DeserializeValidationResult<PresentationCollection>.Failure(this.ValidationFailed(validation));
        }

        return DeserializeValidationResult<PresentationCollection>.Success(deserializedCollection.ConvertedIIIF,
            rawRequestBody);
    }


    [Authorize]
    [HttpDelete("collections/{id}")]
    public async Task<IActionResult> Delete(int customerId, string id)
    {
        if (!Request.HasShowExtraHeader()) return this.Forbidden();

        return await HandleDelete(new DeleteCollection(customerId, id));
    }

    /// <summary> 
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response with 404 status code.
    /// </summary>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    private ObjectResult PresentationUnableToSerialize() =>
        this.PresentationProblem("Could not deserialize collection", null, (int) HttpStatusCode.BadRequest,
            "Deserialization Error");
}