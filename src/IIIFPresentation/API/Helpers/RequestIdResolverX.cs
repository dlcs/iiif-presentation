using API.Features.Common.Helpers;
using API.Infrastructure.Requests;

namespace API.Helpers;

public static class RequestIdResolverX
{
    /// <summary>
    /// Resolves the body's "id" property and, if <paramref name="urlId"/> is supplied, checks it doesn't disagree
    /// with an own-host flat id resolved from the body.
    /// </summary>
    /// <param name="requestIdResolver">Resolver used to resolve the body's "id" property</param>
    /// <param name="customerId">Customer id from the request URL</param>
    /// <param name="bodyId">The body's "id" property</param>
    /// <param name="urlId">
    /// The id from the request URL - only present for an upsert (PUT to a specific id), where it must agree with
    /// any own-host flat id resolved from the body
    /// </param>
    public static (PresentationResult? error, ResolvedRequestId resolvedId) ResolveAndValidate(
        this IRequestIdResolver requestIdResolver, int customerId, string? bodyId, string? urlId = null)
    {
        var resolvedId = requestIdResolver.Resolve(customerId, bodyId);
        if (resolvedId.IsError) return (resolvedId.Error, resolvedId);

        if (urlId != null && resolvedId.FlatId != null && resolvedId.FlatId != urlId)
        {
            return (UpsertErrorHelper.IdMustMatchUrl(), resolvedId);
        }

        return (null, resolvedId);
    }
}
