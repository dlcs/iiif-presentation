using API.Helpers;
using Core.Web;
using DLCS;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace API.Tests.Helpers;

public static class TestPathGenerator
{
    public static HttpRequestBasedPathGenerator CreatePathGenerator(string baseUrl, string scheme)
    {
        var dlcsSettings = Options.Create(new DlcsSettings
            { ApiUri = new Uri("https://dlcs.test") });
        var typedPathTemplateOptions = Options.Create(new TypedPathTemplateOptions()
        {
            Defaults = new Dictionary<string, string>()
            {
                ["ManifestPrivate"] = "{customerId}/manifests/{resourceId}",
                ["CollectionPrivate"] = "{customerId}/collections/{resourceId}",
                ["ResourcePublic"] = "{customerId}/{hierarchyPath}",
                ["Canvas"] = "{customerId}/canvases/{resourceId}",
            }
        });

        var contextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
                {
                    Scheme = scheme,
                    Host = new HostString(baseUrl)
                }
            }
        };

        var http = new HttpRequestBasedPathGenerator(dlcsSettings,
            new ConfigDrivenPresentationPathGenerator(typedPathTemplateOptions, contextAccessor));
        
        return http;
    }
}
