using System.Net;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Models.API.General;

namespace API.Infrastructure.Filters;

/// <summary>
/// Requires request to have the "show extras" header, short-circuiting with a 403 if it is absent.
/// </summary>
/// <remarks>
/// Runs after <see cref="VaryHeaderAttribute"/> (Order 0) so that Vary is still emitted when this
/// short-circuits - whether the response is a 403 or a 200 depends on the header.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireShowExtrasAttribute : ActionFilterAttribute
{
    public RequireShowExtrasAttribute()
    {
        Order = 10;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;
        if (request.HasShowExtraHeader()) return;

        var error = new Error
        {
            // GetDisplayUrl() takes the path as an arg rather than reading Request.Path, so pass it
            // explicitly - without it the instance is just host + query string
            Instance = request.GetDisplayUrl(request.Path),
            Status = (int)HttpStatusCode.Forbidden
        };

        context.Result = new ObjectResult(error) { StatusCode = error.Status };
    }
}
