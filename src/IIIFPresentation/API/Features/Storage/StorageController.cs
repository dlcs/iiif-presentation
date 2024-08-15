using System.Net;
using API.Attributes;
using API.Auth;
using API.Converters;
using API.Features.Storage.Requests;
using API.Infrastructure;
using API.Infrastructure.Requests;
using API.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Models.Response;

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

        if (storageRoot.root == null) return NotFound();

        return Ok( storageRoot.root.ToHierarchicalCollection(GetUrlRoots(), storageRoot.items));
    }
    
    [HttpGet("{*slug}")]
    [EtagCaching]
    public async Task<IActionResult> GetHierarchicalCollection(int customerId, string slug)
    {
        var storageRoot = await Mediator.Send(new GetHierarchicalCollection(customerId, slug));

        if (storageRoot.root == null) return NotFound();

        return Ok( storageRoot.root.ToHierarchicalCollection(GetUrlRoots(), storageRoot.items));
    }
    
    [HttpGet("collections/{id}")]
    [EtagCaching]
    public async Task<IActionResult> Get(int customerId, string id)
    {
        var addAdditionalProperties = Request.Headers.Any(x => x.Key == AdditionalPropertiesHeader) &&
                                      Authorizer.CheckAuthorized(Request);

        var storageRoot = await Mediator.Send(new GetCollection(customerId, id));

        if (storageRoot.root == null) return NotFound();

        return Ok(addAdditionalProperties
            ? storageRoot.root.ToFlatCollection(GetUrlRoots(), Settings.PageSize, storageRoot.items)
            : storageRoot.root.ToHierarchicalCollection(GetUrlRoots(), storageRoot.items));
    }
    
    [HttpPost("collections")]
    [EtagCaching]
    public async Task<IActionResult> Post(int customerId, [FromBody] FlatCollection collection)
    {
        if (!Authorizer.CheckAuthorized(Request))
        {
            return Problem(statusCode: (int)HttpStatusCode.Forbidden);
        }
        
        var created = await Mediator.Send(new CreateCollection(customerId, collection));

        return Ok(created);
    }

    /// <summary>
    /// Used by derived controllers to construct correct fully qualified URLs in returned objects.
    /// </summary>
    /// <returns></returns>
    protected UrlRoots GetUrlRoots()
    {
        return new UrlRoots
        {
            BaseUrl = Request.GetBaseUrl(),
            ResourceRoot = Settings.ResourceRoot.ToString()
        };
    }
}