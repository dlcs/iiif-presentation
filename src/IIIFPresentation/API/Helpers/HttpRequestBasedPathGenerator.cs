using API.Infrastructure.Requests;
using Core.Web;
using DLCS;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace API.Helpers;

public class HttpRequestBasedPathGenerator(IOptions<DlcsSettings> dlcsOptions, 
    IPresentationPathGenerator presentationPathGenerator)
    : PathGeneratorBase(presentationPathGenerator)
{
    protected override Uri DlcsApiUrl { get; } = dlcsOptions.Value.ApiUri;
}
