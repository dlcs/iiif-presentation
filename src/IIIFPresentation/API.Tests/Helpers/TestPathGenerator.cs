using API.Helpers;
using Microsoft.AspNetCore.Http;

namespace API.Tests.Helpers;

public class TestPathGenerator
{
    public static PathGenerator CreatePathGenerator(string baseUrl, string scheme)
    {
        return new PathGenerator(new HttpContextAccessor()
        {
            HttpContext = new DefaultHttpContext()
            {
                Request =
                {
                    Scheme = scheme,
                    Host = new HostString(baseUrl)
                }
            }
        });
    }
}