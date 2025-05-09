using API.Helpers;
using API.Paths;
using Core.Web;
using DLCS;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace API.Tests.Helpers;

public static class TestPathGenerator
{
    public static HttpRequestBasedPathGenerator CreatePathGenerator(string baseUrl, string scheme)
    {
        var dlcsSettings = Options.Create(new DlcsSettings
            { ApiUri = new Uri("https://dlcs.test") });
        var typedPathTemplateOptions = Options.Create(new TypedPathTemplateOptions());

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
