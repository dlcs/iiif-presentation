using System.Net;
using API.Auth;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Features.Storage.Requests;
using API.Features.Storage.Validators;
using API.Helpers;
using API.Infrastructure;
using API.Infrastructure.Filters;
using API.Infrastructure.Helpers;
using API.Infrastructure.Http;
using API.Infrastructure.Requests;
using API.Settings;
using Core.Helpers;
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
    IETagCache eTagCache,
    ILogger<CollectionController> logger)
    : PresentationController(options.Value, mediator, eTagCache, logger)
{
    [HttpGet("collections/{id}")]
    [VaryHeader]
    public async Task<IActionResult> Get(int customerId, string id, int? page = 1, int? pageSize = -1,
        string? orderBy = null, string? orderByDescending = null)
    {
        var orderByField = this.GetOrderBy(orderBy, orderByDescending, out var descending);

        var entityResult = await Mediator.Send(new GetCollection(id, Request.Headers.IfNoneMatch.AsETagValues(), page,
            pageSize, orderByField, descending));

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
    [RequireShowExtras]
    [HttpGet("collections/{id}/search")]
    [VaryHeader]
    public async Task<IActionResult> Search(int customerId, string id, string? label = null, int? page = 1,
        int? pageSize = -1, string? orderBy = null, string? orderByDescending = null)
    {
        // MVP: only the root collection supports search-across
        if (!KnownCollections.IsRoot(id)) return this.PresentationNotFound();

        // Validate search term length, at least one must match minimum length
        var term = label?.Trim() ?? string.Empty;
        var terms = term.SplitOnWhitespace();
        if (!terms.Any(t => t.Length >= Settings.MinSearchLength))
        {
            return this.PresentationProblem(
                $"At least one search term must be {Settings.MinSearchLength} characters or more",
                null, (int)HttpStatusCode.BadRequest, "Bad request",
                this.GetErrorType(ModifyCollectionType.InvalidSearchQuery));
        }

        var orderByField = this.GetOrderBy(orderBy, orderByDescending, out var descending);

        return await HandleFetch(new SearchCollection(id, term, terms, page, pageSize, orderByField, descending),
            errorTitle: "Search failed");
    }

    [Authorize]
    [RequireShowExtras]
    [HttpPost("collections")]
    public async Task<IActionResult> Post(int customerId, [FromServices] PresentationValidator validator,
        [FromServices] IRequestIdResolver requestIdResolver)
    {
        var deserializeValidationResult = await DeserializeAndValidate(validator, null, null);
        if (deserializeValidationResult.HasError) return deserializeValidationResult.Error;

        var resolvedId = requestIdResolver.Resolve(customerId, deserializeValidationResult.ConvertedIIIF.Id);
        if (resolvedId.IsError) return this.ModifyResultToHttpResult(resolvedId.Error!, null, "Operation failed");

        return await HandleUpsert(new CreateCollection(customerId,
            deserializeValidationResult.ConvertedIIIF, deserializeValidationResult.RawRequestBody,
            urlParentPath: resolvedId.HierarchicalParentPath, clientProvidedId: resolvedId.FlatId));
    }

    [Authorize]
    [RequireShowExtras]
    [HttpPut("collections/{id}")]
    public async Task<IActionResult> Put(int customerId, string id,
        [FromServices] RootCollectionValidator rootValidator,
        [FromServices] PresentationValidator presentationValidator,
        [FromServices] IRequestIdResolver requestIdResolver)
    {
        var deserializeValidationResult = await DeserializeAndValidate(presentationValidator, id, rootValidator);
        if (deserializeValidationResult.HasError) return deserializeValidationResult.Error;

        var resolvedId = requestIdResolver.Resolve(customerId, deserializeValidationResult.ConvertedIIIF.Id);
        if (resolvedId.IsError) return this.ModifyResultToHttpResult(resolvedId.Error!, null, "Operation failed");
        if (resolvedId.FlatId != null && resolvedId.FlatId != id)
            return this.ModifyResultToHttpResult(UpsertErrorHelper.IdMustMatchUrl(), null, "Operation failed");

        return await HandleUpsert(new UpsertCollection(customerId, id,
            deserializeValidationResult.ConvertedIIIF, Request.Headers.IfMatch,
            deserializeValidationResult.RawRequestBody,
            urlParentPath: resolvedId.HierarchicalParentPath, urlSlug: resolvedId.Slug),
            invalidatesEtag: Request.Headers.IfMatch);
    }


    private async Task<DeserializeValidationResult<PresentationCollection>> DeserializeAndValidate(
        PresentationValidator presentationValidator, string? id, RootCollectionValidator? rootValidator)
    {
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
    [RequireShowExtras]
    [HttpDelete("collections/{id}")]
    public async Task<IActionResult> Delete(int customerId, string id)
    {
        return await HandleDelete(new DeleteCollection(customerId, id, Request.Headers.IfMatch));
    }

    /// <summary> 
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="ObjectResult"/> response with 400 status code.
    /// </summary>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    private ObjectResult PresentationUnableToSerialize() =>
        this.PresentationProblem("Could not deserialize collection", null, (int)HttpStatusCode.BadRequest,
            "Deserialization Error", this.GetErrorType(ModifyCollectionType.CannotDeserialize));
}
