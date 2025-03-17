using System.Net.Http.Headers;
using API.Infrastructure.Helpers;

namespace API.Tests.Integration.Infrastructure;

public static class EtagManagerX
{
    public static void SetCorrectEtag(this IETagManager etagManager, HttpRequestMessage requestMessage, string manifestId, 
        int customerId, bool isCollection = false)
    {
        // This saves some boilerplate by correctly setting Etag in manager and request
        var tag = $"\"{manifestId}\"";
        etagManager.UpsertETag($"/{customerId}/{(isCollection ? "collections" : "manifests")}/{manifestId}", tag);
        requestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue(tag));
    }
}
