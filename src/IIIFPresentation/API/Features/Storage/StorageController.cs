using System.Net;
using API.Attributes;
using API.Auth;
using API.Converters;
using API.Features.Storage.Requests;
using API.Features.Storage.Validators;
using API.Helpers;
using API.Infrastructure;
using API.Infrastructure.Filters;
using API.Infrastructure.Helpers;
using API.Settings;
using Core.IIIF;
using IIIF.Presentation;
using IIIF.Serialisation;
using IIIF.Serialisation.Deserialisation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Newtonsoft.Json;

namespace API.Features.Storage;

[Route("/{customerId:int}")]
[ApiController]
public class StorageController(IAuthenticator authenticator, IOptions<ApiSettings> options, IMediator mediator)
    : PresentationController(options.Value, mediator)
{
    private JsonSerializerSettings jsonSettings;
    
    [HttpGet("{*slug}")]
    [ETagCaching]
    [VaryHeader]
    public async Task<IActionResult> GetHierarchicalCollection(int customerId, string slug = "")
    {
        var storageRoot = await Mediator.Send(new GetHierarchicalCollection(customerId, slug));

        if (storageRoot.Collection is not { IsPublic: true }) return this.PresentationNotFound();

        if (Request.HasShowExtraHeader() && await authenticator.ValidateRequest(Request) == AuthResult.Success)
        {
            return SeeOther(storageRoot.Collection.GenerateFlatCollectionId(GetUrlRoots()));
        }

        return storageRoot.StoredCollection == null
            ? Content(storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items).AsJson(), 
                ContentTypes.V3)
            : Content(storageRoot.StoredCollection, ContentTypes.V3);
    }
    
    [Authorize]
    [HttpPost("{*slug}")]
    [ETagCaching]
    public async Task<IActionResult> PostHierarchicalCollection(int customerId, string slug)
    {
        // X-IIIF-CS-Show-Extras is not required here, the body should be vanilla json
        using var streamReader = new StreamReader(Request.Body);
        
        var rawRequestBody = await streamReader.ReadToEndAsync();
        
        return await HandleUpsert(new PostHierarchicalCollection(customerId, slug, GetUrlRoots(), rawRequestBody));
    }
    
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
            await Mediator.Send(new GetCollection(customerId, id, page.Value, pageSize.Value, orderByField, descending));

        if (storageRoot.Collection == null) return this.PresentationNotFound();

        if (Request.HasShowExtraHeader() && await authenticator.ValidateRequest(Request) == AuthResult.Success)
        {
            var orderByParameter = orderByField != null
                ? $"{(descending ? nameof(orderByDescending) : nameof(orderBy))}={orderByField}" 
                : null;

            return Ok(storageRoot.Collection.ToFlatCollection(GetUrlRoots(), pageSize.Value, page.Value,
                storageRoot.TotalItems, storageRoot.Items, orderByParameter));
        }

        return SeeOther(storageRoot.Collection.GenerateHierarchicalCollectionId(GetUrlRoots()));
    }

    [Authorize]
    [HttpPost("collections")]
    [ETagCaching]
    public async Task<IActionResult> Post(int customerId, [FromServices] PresentationCollectionValidator validator)
    {
        if (!Request.HasShowExtraHeader())
        {
            return this.PresentationProblem(statusCode: (int)HttpStatusCode.Forbidden);
        }
        
        using var streamReader = new StreamReader(Request.Body);

        var rawRequestBody = await streamReader.ReadToEndAsync();

        var collection = await rawRequestBody.ToPresentation<PresentationCollection>();

        if (collection == null) return PresentationUnableToSerialize();

        var validation = await validator.ValidateAsync(collection);
        
        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }
        
        return await HandleUpsert(new CreateCollection(customerId, collection, rawRequestBody, GetUrlRoots()));
    }
    
    [Authorize]
    [HttpPut("collections/{id}")]
    [ETagCaching]
    public async Task<IActionResult> Put(int customerId, string id, 
        [FromServices] PresentationCollectionValidator validator)
    {
        if (!Request.HasShowExtraHeader())
        {
            return this.PresentationProblem(statusCode: (int)HttpStatusCode.Forbidden);
        }
        
        using var streamReader = new StreamReader(Request.Body);
        var rawRequestBody = await streamReader.ReadToEndAsync();
        
        var collection = await rawRequestBody.ToPresentation<PresentationCollection>();

        if (collection == null) return PresentationUnableToSerialize();

        var validation = await validator.ValidateAsync(collection);

        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        return await HandleUpsert(new UpsertCollection(customerId, id, collection, GetUrlRoots(),
            Request.Headers.IfMatch, rawRequestBody));
    }
    
    [Authorize]
    [HttpDelete("collections/{id}")]
    public async Task<IActionResult> Delete(int customerId, string id)
    {
        if (!Request.HasShowExtraHeader())
        {
            return this.PresentationProblem(statusCode: (int)HttpStatusCode.Forbidden);
        }

        return await HandleDelete(new DeleteCollection(customerId, id));
    }

    private StatusCodeResult SeeOther(string location)
    {
        Response.Headers.Location = location;

        return StatusCode((int)HttpStatusCode.SeeOther);
    } 
    
    /// <summary> 
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response with 404 status code.
    /// </summary>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    private ObjectResult PresentationUnableToSerialize()
    {
        return this.PresentationProblem("Could not deserialize collection", null, (int)HttpStatusCode.BadRequest,
            "Deserialization Error");
    }
}