using Core.Web;

namespace Test.Helpers.Helpers;

public static class PathRewriteOptions
{
    public static TypedPathTemplateOptions Default = new ()
    {
        Defaults = new Dictionary<string, string>
        {
            ["ManifestPrivate"] = "/{customerId}/manifests/{resourceId}",
            ["CollectionPrivate"] = "/{customerId}/collections/{resourceId}",
            ["ResourcePublic"] = "/{customerId}/{hierarchyPath}",
            ["Canvas"] = "/{customerId}/canvases/{resourceId}"
        },
        Overrides =
        {
            // override everything
            ["foo.com"] = new Dictionary<string, string>
            {
                ["ManifestPrivate"] = "/foo/{customerId}/manifests/{resourceId}",
                ["CollectionPrivate"] = "/foo/{customerId}/collections/{resourceId}",
                ["ResourcePublic"] = "/foo/{customerId}/{hierarchyPath}",
                ["Canvas"] = "/foo/{customerId}/canvases/{resourceId}"
            },
            // fallback to defaults
            ["no-customer.com"] = new Dictionary<string, string>
            {
                ["ManifestPrivate"] = "/manifests/{resourceId}",
                ["CollectionPrivate"] = "/collections/{resourceId}",
                ["ResourcePublic"] = "/{hierarchyPath}",
                ["Canvas"] = "/canvases/{resourceId}"
            },
            ["additional-path-no-customer.com"] = new Dictionary<string, string>
            {
                ["ManifestPrivate"] = "/foo/manifests/{resourceId}",
                ["CollectionPrivate"] = "/foo/collections/{resourceId}",
                ["ResourcePublic"] = "/foo/{hierarchyPath}",
                ["Canvas"] = "/foo/canvases/{resourceId}"
            },
            // custom base URL
            ["fully-qualified.com"] = new Dictionary<string, string>
            {
                ["ManifestPrivate"] = "https://foo.com/{customerId}/manifests/{resourceId}",
                ["CollectionPrivate"] = "https://foo.com/{customerId}/collections/{resourceId}",
                ["ResourcePublic"] = "https://foo.com/{customerId}/{hierarchyPath}",
                ["Canvas"] = "https://foo.com/{customerId}/canvases/{resourceId}"
            }
        }
    };
}
