using System.Net;
using API.Attributes;
using API.Auth;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Features.Storage.Requests;
using API.Features.Storage.Validators;
using API.Infrastructure;
using API.Infrastructure.Filters;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Models;
using Models.API.Collection;
using Models.API.General;

namespace API.Features.Storage;

[Route("/{customerId:int}")]
[ApiController]
public class CollectionController(
    IAuthenticator authenticator,
    IOptions<ApiSettings> options,
    IMediator mediator,
    ILogger<CollectionController> logger)
    : PresentationController(options.Value, mediator, logger)
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

        var entityResult =
            await Mediator.Send(new GetCollection(customerId, id, page.Value, pageSize.Value, orderByField,
                descending));
        
        if (entityResult.Error)
            return this.PresentationProblem(entityResult.ErrorMessage,
                statusCode: (int) HttpStatusCode.InternalServerError);
        
        if (Request.HasShowExtraHeader() && await authenticator.ValidateRequest(Request) == AuthResult.Success)
        {
            return entityResult.EntityNotFound ? this.PresentationNotFound() : this.PresentationContent(entityResult.Entity!, etag: entityResult.ETag);
        }

        return entityResult.Entity?.Behavior.IsPublic() ?? false
            ? SeeOther(entityResult.Entity.PublicId!)
            : this.PresentationNotFound();
    }

    [Authorize]
    [HttpPost("collections")]
    [ETagCaching]
    public async Task<IActionResult> Post(int customerId, [FromServices] PresentationValidator validator)
    {
        var deserializeValidationResult = await DeserializeAndValidate(validator, null, null);
        if (deserializeValidationResult.HasError) return deserializeValidationResult.Error;

        return await HandleUpsert(new CreateCollection(customerId,
            deserializeValidationResult.ConvertedIIIF, deserializeValidationResult.RawRequestBody));
    }

    [Authorize]
    [HttpPut("collections/{id}")]
    [ETagCaching]
    public async Task<IActionResult> Put(int customerId, string id,
        [FromServices] RootCollectionValidator rootValidator, 
        [FromServices] PresentationValidator presentationValidator)
    {
        var deserializeValidationResult = await DeserializeAndValidate(presentationValidator, id, rootValidator);
        if (deserializeValidationResult.HasError) return deserializeValidationResult.Error;

        return await HandleUpsert(new UpsertCollection(customerId, id,
            deserializeValidationResult.ConvertedIIIF, Request.Headers.IfMatch,
            deserializeValidationResult.RawRequestBody));
    }


    private async Task<DeserializeValidationResult<PresentationCollection>> DeserializeAndValidate(
        PresentationValidator presentationValidator, string? id, RootCollectionValidator? rootValidator)
    {
        if (!Request.HasShowExtraHeader())
        {
            return DeserializeValidationResult<PresentationCollection>.Failure(this.Forbidden());
        }
        
        var rawRequestBody = await Request.GetRawRequestBodyAsync();
        
        var deserializedCollection =
            await rawRequestBody.TryDeserializePresentation<PresentationCollection>(logger);
        if (deserializedCollection.Error)
        {
            return DeserializeValidationResult<PresentationCollection>.Failure(PresentationUnableToSerialize());
        }

        var validation = id != null && KnownCollections.IsRoot(id)
            ? rootValidator!.Validate(deserializedCollection.ConvertedIIIF)
            : presentationValidator.Validate(deserializedCollection.ConvertedIIIF);

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
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="ObjectResult"/> response with 400 status code.
    /// </summary>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    private ObjectResult PresentationUnableToSerialize() => 
        this.PresentationProblem("Could not deserialize collection", null, (int) HttpStatusCode.BadRequest,
        "Deserialization Error", this.GetErrorType(ModifyCollectionType.CannotDeserialize));
}
