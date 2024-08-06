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

[Route("/{customerId}/collections")]
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
    public async Task<IActionResult> Get(int customerId)
    {
        var addAdditionalProperties = Request.Headers.Any(x => x.Key == AdditionalPropertiesHeader) &&
                                   Authorizer.CheckAuthorized(Request);

        var storageRoot = await Mediator.Send(new GetStorageRoot(customerId));

        if (storageRoot == null) return NotFound();

        return Ok(addAdditionalProperties
            ? storageRoot.ToFlatCollection(GetUrlRoots(), Settings.PageSize, new List<Item>())
            : storageRoot.ToHierarchicalCollection(GetUrlRoots()));
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