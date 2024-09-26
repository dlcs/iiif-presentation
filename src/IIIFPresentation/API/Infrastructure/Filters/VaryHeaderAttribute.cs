using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace API.Infrastructure.Filters;

public class VaryHeaderAttribute : ActionFilterAttribute
{
    private static readonly string[] VaryHeaders = ["X-IIIF-CS-Show-Extras", "Authorization"];

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is not null)
            context.HttpContext.Response.Headers.Append(HeaderNames.Vary, VaryHeaders);
    }
}