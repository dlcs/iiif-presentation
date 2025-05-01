using Core.Helpers;
using Microsoft.AspNetCore.Http;

namespace Core.Web;

public static class HttpContextAccessorX
{
    /// <summary>
    /// Safely access the IHttpContextAccessor.HttpContext property, throwing an exception if null
    /// </summary>
    public static HttpContext SafeHttpContext(this IHttpContextAccessor httpContextAccessor)
        => httpContextAccessor.HttpContext.ThrowIfNull(nameof(httpContextAccessor.HttpContext));
}
