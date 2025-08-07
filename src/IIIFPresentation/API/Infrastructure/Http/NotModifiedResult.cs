using System.Net;
using Core.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace API.Infrastructure.Http;

public class NotModifiedResult(Guid etag) : ActionResult
{
    private static readonly string[] HeadersToKeepFor304 =
    {
        HeaderNames.CacheControl,
        HeaderNames.ContentLocation,
        HeaderNames.ETag,
        HeaderNames.Expires,
        HeaderNames.Vary
    };

    public override void ExecuteResult(ActionContext context)
    {
        var response = context.ThrowIfNull(nameof(context)).HttpContext.Response;
        var responseHeaders = response.GetTypedHeaders();

        responseHeaders.ETag ??= new EntityTagHeaderValue($"\"{etag:N}\"");

        // The no-cache response directive indicates that the response can be stored in caches,
        // but the response must be validated with the origin server before each reuse,
        // even when the cache is disconnected from the origin server.
        responseHeaders.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true
        };

        // Remove all unnecessary headers while only keeping the ones that should be included in a `304` response.
        foreach (var header in response.Headers)
            if (!HeadersToKeepFor304.Contains(header.Key))
            {
                response.Headers.Remove(header.Key);
            }

        response.StatusCode = (int)HttpStatusCode.NotModified;
    }
}
