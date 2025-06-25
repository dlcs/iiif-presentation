using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace API.Attributes;

public class ETagCachingAttribute : ActionFilterAttribute
{
    // When a "304 Not Modified" response is to be sent back to the client, all headers apart from the following list should be stripped from the response to keep the response size minimal. See https://datatracker.ietf.org/doc/html/rfc7232#section-4.1:~:text=200%20(OK)%20response.-,The%20server%20generating%20a%20304,-response%20MUST%20generate
    private static readonly string[] HeadersToKeepFor304 =
    {
        HeaderNames.CacheControl,
        HeaderNames.ContentLocation,
        HeaderNames.ETag,
        HeaderNames.Expires,
        HeaderNames.Vary
    };

    // Adds cache headers to response
    public override async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next
    )
    {
        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;

        if (response.StatusCode is StatusCodes.Status200OK or StatusCodes.Status201Created or StatusCodes.Status202Accepted)
        {
            var responseHeaders = response.GetTypedHeaders();

            // The no-cache response directive indicates that the response can be stored in caches,
            // but the response must be validated with the origin server before each reuse,
            // even when the cache is disconnected from the origin server.
            responseHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true
            };
            
            // This request generates a hash from the response - this would come from S3 in live
            responseHeaders.ETag ??=
                context.HttpContext.Items["__etag"] is Guid etag ? new EntityTagHeaderValue($"\"{etag:N}\"") : null;

            var requestHeaders = request.GetTypedHeaders();

            if (IsClientCacheValid(requestHeaders, responseHeaders))
            {
                response.StatusCode = StatusCodes.Status304NotModified;

                // Remove all unnecessary headers while only keeping the ones that should be included in a `304` response.
                foreach (var header in response.Headers)
                    if (!HeadersToKeepFor304.Contains(header.Key))
                    {
                        response.Headers.Remove(header.Key);
                    }

                return;
            }
        }

        _ = await next();
    }
    

    private static bool IsClientCacheValid(RequestHeaders reqHeaders, ResponseHeaders resHeaders)
    {
        // If both `If-None-Match` and `If-Modified-Since` are present in a request, `If-None-Match` takes precedence and `If-Modified-Since` is ignored (provided, of course, that the resource supports entity-tags, hence the second condition after the `&&` operator in the following `if`). See https://datatracker.ietf.org/doc/html/rfc7232#section-3.3:~:text=A%20recipient%20MUST%20ignore%20If%2DModified%2DSince%20if
        if (reqHeaders.IfNoneMatch.Any() && resHeaders.ETag is not null)
            return reqHeaders.IfNoneMatch.Any(etag =>
                etag.Compare(resHeaders.ETag,
                    false)
            );

        if (reqHeaders.IfModifiedSince is not null && resHeaders.LastModified is not null)
        {
            return reqHeaders.IfModifiedSince >= resHeaders.LastModified;
        }

        return false;
    }
}
