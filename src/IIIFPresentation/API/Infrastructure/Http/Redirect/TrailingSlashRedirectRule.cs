using Microsoft.AspNetCore.Rewrite;

namespace API.Infrastructure.Http.Redirect;

/// <summary>
///     This application specific rules about redirecting trailing slashes.
///     Encapsulates the built-in RedirectRule, but only allows execution
///     for GET requests - it was causing issues with PUT/POST/DELETE etc.
/// </summary>
public class TrailingSlashRedirectRule : IRule
{
    // Use built-in logic, it's internal so this is the way
    private static readonly IRule BaseRedirectRule
        = new RewriteOptions().AddRedirect("(.*)/$", "$1").Rules.Single();

    #region Implementation of IRule

    public void ApplyRule(RewriteContext context)
    {
        // Only for GET requests
        if ("GET".Equals(context.HttpContext.Request.Method, StringComparison.OrdinalIgnoreCase))
            BaseRedirectRule.ApplyRule(context);
    }

    #endregion
}
