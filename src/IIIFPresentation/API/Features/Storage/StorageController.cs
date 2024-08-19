using System.Net;
using API.Attributes;
using API.Auth;
using API.Converters;
using API.Features.Storage.Requests;
using API.Features.Storage.Validators;
using API.Infrastructure;
using API.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using FluentValidation;
using Models.API.Collection;

namespace API.Features.Storage;

[Route("/{customerId}")]
[ApiController]
public class StorageController : PresentationController
{
    private const string AdditionalPropertiesHeader = "IIIF-CS-Show-Extra";

    public StorageController(IOptions<ApiSettings> options, IMediator mediator) : base(options.Value, mediator)
    {
    }
    
    [HttpGet]
    [EtagCaching]
    public async Task<IActionResult> GetHierarchicalRootCollection(int customerId)
    {
        var storageRoot = await Mediator.Send(new GetCollection(customerId, "root"));

        if (storageRoot.Collection == null) return NotFound();

        return Ok( storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items));
    }
    
    [HttpGet("{*slug}")]
    [EtagCaching]
    public async Task<IActionResult> GetHierarchicalCollection(int customerId, string slug)
    {
        var storageRoot = await Mediator.Send(new GetHierarchicalCollection(customerId, slug));

        if (storageRoot.Collection == null) return NotFound();

        return Ok( storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items));
    }
    
    [HttpGet("collections/{id}")]
    [EtagCaching]
    public async Task<IActionResult> Get(int customerId, string id)
    {
        var addAdditionalProperties = Request.Headers.Any(x => x.Key == AdditionalPropertiesHeader) &&
                                      Authorizer.CheckAuthorized(Request);

        var storageRoot = await Mediator.Send(new GetCollection(customerId, id));

        if (storageRoot.Collection == null) return NotFound();

        return Ok(addAdditionalProperties
            ? storageRoot.Collection.ToFlatCollection(GetUrlRoots(), Settings.PageSize, storageRoot.Items)
            : storageRoot.Collection.ToHierarchicalCollection(GetUrlRoots(), storageRoot.Items));
    }
    
    [HttpPost("collections")]
    [EtagCaching]
    public async Task<IActionResult> Post(int customerId, [FromBody] FlatCollection collection, [FromServices] FlatCollectionValidator validator)
    {
        if (!Authorizer.CheckAuthorized(Request))
        {
            return Problem(statusCode: (int)HttpStatusCode.Forbidden);
        }

        var validation = await validator.ValidateAsync(collection, policy => policy.IncludeRuleSets("create"));

        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        var created = await Mediator.Send(new CreateCollection(customerId, collection, GetUrlRoots()));

        return Ok(created);
    }
}