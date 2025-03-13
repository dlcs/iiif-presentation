using System.Net.Http.Headers;
using API.Infrastructure.Helpers;

namespace API.Tests.Integration.Infrastructure;

public static class EtagHelper
{
    public static void SetCorrectEtag(HttpRequestMessage requestMessage, string manifestId, IETagManager etagManager, 
        int customerId)
    {
        // This saves some boilerplate by correctly setting Etag in manager and request
        var tag = $"\"{manifestId}\"";
        etagManager.UpsertETag($"/{customerId}/manifests/{manifestId}", tag);
        requestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue(tag));
    }
}
