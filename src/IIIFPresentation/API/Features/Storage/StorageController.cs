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
using FluentValidation;
using Models.API.Collection;
using Models.API.Collection.Update;

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

        return Ok(storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items));
    }
    
    [HttpGet("{*slug}")]
    [EtagCaching]
    public async Task<IActionResult> GetHierarchicalCollection(int customerId, string slug)
    {
        var storageRoot = await Mediator.Send(new GetHierarchicalCollection(customerId, slug));

        if (storageRoot.Collection is not { IsPublic: true }) return NotFound();

        return Ok(storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items));
    }
    
    [HttpGet("collections/{id}")]
    [EtagCaching]
    public async Task<IActionResult> Get(int customerId, string id)
    {
        var storageRoot = await Mediator.Send(new GetCollection(customerId, id));

        if (storageRoot.Collection == null) return NotFound();

        return Ok(Request.ShowExtraProperties()
            ? storageRoot.Collection.ToFlatCollection(GetUrlRoots(), Settings.PageSize, storageRoot.Items)
            : storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items));
    }
    
    [HttpPost("collections")]
    [EtagCaching]
    public async Task<IActionResult> Post(int customerId, [FromBody] FlatCollection collection, [FromServices] FlatCollectionValidator validator)
    {
        if (!Request.ShowExtraProperties())
        {
            return Problem(statusCode: (int)HttpStatusCode.Forbidden);
        }

        var validation = await validator.ValidateAsync(collection, policy => policy.IncludeRuleSets("create"));

        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        return await HandleUpsert(new CreateCollection(customerId, collection, GetUrlRoots()));
    }
    
    [HttpPut("collections/{id}")]
    [EtagCaching]
    public async Task<IActionResult> Put(int customerId, string id, [FromBody] UpdateFlatCollection collection, 
        [FromServices] UpdateFlatCollectionValidator validator)
    {
        if (!Request.ShowExtraProperties())
        {
            return Problem(statusCode: (int)HttpStatusCode.Forbidden);
        }

        var validation = await validator.ValidateAsync(collection, policy => policy.IncludeRuleSets("update"));

        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        return await HandleUpsert(new UpdateCollection(customerId, id, collection, GetUrlRoots()));
    }
}