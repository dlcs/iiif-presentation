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
    public async Task<IActionResult> GetHierarchicalRootCollection(int customerId)
    {
        var storageRoot = await Mediator.Send(new GetCollection(customerId, "root"));

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
    public async Task<IActionResult> Get(int customerId, string id)
    {
        var storageRoot = await Mediator.Send(new GetCollection(customerId, id));

        if (storageRoot.Collection == null) return NotFound();

        if (Request.ShowExtraProperties())
        {
            return Ok(storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items));
        }

        return Content(storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items).AsJson(),
            ContentTypes.V3);
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