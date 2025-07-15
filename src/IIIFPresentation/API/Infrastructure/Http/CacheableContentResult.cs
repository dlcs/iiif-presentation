using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace API.Infrastructure.Http;

public class CacheableContentResult : ContentResult
{
    public required Guid ETag { get; set; }

    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (StatusCode == (int)HttpStatusCode.Accepted)
            return base.ExecuteResultAsync(context);
        
        var response = context.HttpContext.Response;
        var responseHeaders = response.GetTypedHeaders();

        responseHeaders.ETag ??= new EntityTagHeaderValue($"\"{ETag:N}\"");

        // The no-cache response directive indicates that the response can be stored in caches,
        // but the response must be validated with the origin server before each reuse,
        // even when the cache is disconnected from the origin server.
        responseHeaders.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true
        };

        return base.ExecuteResultAsync(context);
    }
}
