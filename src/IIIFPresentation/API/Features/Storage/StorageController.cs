using System.Net;
using API.Attributes;
using API.Converters;
using API.Features.Storage.Requests;
using API.Features.Storage.Validators;
using API.Infrastructure;
using API.Infrastructure.Helpers;
using API.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using IIIF.Presentation;
using IIIF.Serialisation;
using Models.API.Collection.Upsert;

namespace API.Features.Storage;

[Route("/{customerId}")]
[ApiController]
public class StorageController(IOptions<ApiSettings> options, IMediator mediator)
    : PresentationController(options.Value, mediator)
{
    [HttpGet]
    [EtagCaching]
    public async Task<IActionResult> GetHierarchicalRootCollection(int customerId, int? page = 1, int? pageSize = -1, 
        string? orderBy = null, string? orderByDescending = null)
    {
        (page, pageSize, var descending) = SetPaging(page, pageSize, orderBy, orderByDescending);
        var storageRoot =
            await Mediator.Send(new GetCollection(customerId, "root", page.Value, pageSize.Value, orderBy, descending));

        if (storageRoot.Collection == null) return NotFound();

        return Content(storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items).AsJson(),
            ContentTypes.V3);
    }
    
    [HttpGet("{*slug}")]
    [EtagCaching]
    public async Task<IActionResult> GetHierarchicalCollection(int customerId, string slug)
    {
        var storageRoot = await Mediator.Send(new GetHierarchicalCollection(customerId, slug));

        if (storageRoot.Collection is not { IsPublic: true }) return NotFound();

        return Content(storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items).AsJson(),
            ContentTypes.V3);
    }

    [HttpGet("collections/{id}")]
    [EtagCaching]
    public async Task<IActionResult> Get(int customerId, string id, int? page = 1, int? pageSize = -1, 
        string? orderBy = null, string? orderByDescending = null)
    {
        (page, pageSize, var descending) = SetPaging(page, pageSize, orderBy, orderByDescending);
        var storageRoot =
            await Mediator.Send(new GetCollection(customerId, id, page.Value, pageSize.Value, orderBy, descending));

        if (storageRoot.Collection == null) return NotFound();

        if (Request.ShowExtraProperties())
        {
            return Ok(storageRoot.Collection.ToFlatCollection(GetUrlRoots(), pageSize.Value, page.Value, storageRoot.TotalItems, storageRoot.Items));
        }

        return Content(storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items).AsJson(),
            ContentTypes.V3);
    }

    private (int page, int pageSize, bool descending) SetPaging(int? page, int? pageSize, string? orderBy = null, 
        string? orderByDescending = null)
    {
        if (pageSize is null or <= 0) pageSize = Settings.PageSize;
        if (pageSize > Settings.MaxPageSize) pageSize = Settings.MaxPageSize;
        if (page is null or <= 0) page = 1;
        
        var orderByField = this.GetOrderBy(orderBy, orderByDescending, out var descending);
        
        return (page.Value, pageSize.Value, descending);
    }

    [HttpPost("collections")]
    [EtagCaching]
    public async Task<IActionResult> Post(int customerId, [FromBody] UpsertFlatCollection collection, 
        [FromServices] UpsertFlatCollectionValidator validator)
    {
        if (!Request.ShowExtraProperties())
        {
            return Problem(statusCode: (int)HttpStatusCode.Forbidden);
        }

        var validation = await validator.ValidateAsync(collection);

        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        return await HandleUpsert(new CreateCollection(customerId, collection, GetUrlRoots()));
    }
    
    [HttpPut("collections/{id}")]
    [EtagCaching]
    public async Task<IActionResult> Put(int customerId, string id, [FromBody] UpsertFlatCollection collection, 
        [FromServices] UpsertFlatCollectionValidator validator)
    {
        if (!Request.ShowExtraProperties())
        {
            return Problem(statusCode: (int)HttpStatusCode.Forbidden);
        }

        var validation = await validator.ValidateAsync(collection);

        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        return await HandleUpsert(new UpdateCollection(customerId, id, collection, GetUrlRoots()));
    }
}