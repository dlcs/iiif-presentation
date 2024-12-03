using API.Helpers;
using DLCS;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace API.Tests.Helpers;

public static class TestPathGenerator
{
    public static PathGenerator CreatePathGenerator(string baseUrl, string scheme) 
        => new(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
                {
                    Scheme = scheme,
                    Host = new HostString(baseUrl)
                }
            }
        }, Options.Create(new DlcsSettings { ApiUri = new Uri("https://dlcs.test") }));
}