using Core.Helpers;

namespace API.Helpers;

public static class HttpContextAccessorX
{
    /// <summary>
    /// Safely access the IHttpContextAccessor.HttpContext property, throwing an exception if null
    /// </summary>
    public static HttpContext SafeHttpContext(this IHttpContextAccessor httpContextAccessor)
        => httpContextAccessor.HttpContext.ThrowIfNull(nameof(httpContextAccessor.HttpContext));
}
