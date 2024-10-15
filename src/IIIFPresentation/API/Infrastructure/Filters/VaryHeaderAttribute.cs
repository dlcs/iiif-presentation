using API.Infrastructure.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace API.Infrastructure.Filters;

public class VaryHeaderAttribute : ActionFilterAttribute
{
    private static readonly string[] VaryHeaders = [CustomHttpHeaders.ShowExtras, "Authorization"];

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is not null)
            context.HttpContext.Response.Headers.Append(HeaderNames.Vary, VaryHeaders);
    }
}