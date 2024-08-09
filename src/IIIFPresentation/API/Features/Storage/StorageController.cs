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

    // GET: api/<StorageController>
    [HttpGet]
    [EtagCaching]
    public async Task<IActionResult> GetFlatCollection(int customerId)
    {
        var storageRoot = await Mediator.Send(new GetStorageRoot(customerId));

        if (storageRoot.root == null) return NotFound();

        return Ok( storageRoot.root.ToHierarchicalCollection(GetUrlRoots(), storageRoot.items));
    }
    
    [HttpGet("collections/{*slug}")]
    [EtagCaching]
    public async Task<IActionResult> Get(int customerId, string slug)
    {
        var addAdditionalProperties = Request.Headers.Any(x => x.Key == AdditionalPropertiesHeader) &&
                                      Authorizer.CheckAuthorized(Request);

        var storageRoot = await Mediator.Send(new GetCollection(customerId, slug));

        if (storageRoot.root == null) return NotFound();

        return Ok(addAdditionalProperties
            ? storageRoot.root.ToFlatCollection(GetUrlRoots(), Settings.PageSize, storageRoot.items)
            : storageRoot.root.ToHierarchicalCollection(GetUrlRoots(), storageRoot.items));
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