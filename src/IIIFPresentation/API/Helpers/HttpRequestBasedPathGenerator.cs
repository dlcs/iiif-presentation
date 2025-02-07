using API.Infrastructure.Requests;
using DLCS;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace API.Helpers;

public class HttpRequestBasedPathGenerator(IHttpContextAccessor contextAccessor, IOptions<DlcsSettings> dlcsOptions)
    : PathGeneratorBase
{
    protected override string PresentationUrl { get; } = contextAccessor.HttpContext!.Request.GetBaseUrl();
    protected override Uri DlcsApiUrl { get; } = dlcsOptions.Value.ApiUri;
}
