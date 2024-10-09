using API.Features.Storage.Requests;

namespace API.Features.Storage.Helpers;

public static class PostHierarchicalCollectionX
{
    public static string GetCollectionId(this PostHierarchicalCollection request)
    {
        return $"{request.UrlRoots.BaseUrl}/{request.CustomerId}/{request.Slug}";
    }
}