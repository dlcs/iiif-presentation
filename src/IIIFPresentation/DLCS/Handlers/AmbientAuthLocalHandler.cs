using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace DLCS.Handlers;

public class AmbientAuthLocalHandler() : DelegatingHandler
{
    private readonly DlcsSettings dlcsSettings;
    
    public AmbientAuthLocalHandler(IOptions<DlcsSettings> dlcsOptions) : this()
    {
        dlcsSettings = dlcsOptions.Value;
    }
    
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", dlcsSettings.ApiLocalAuth);
        return base.SendAsync(request, cancellationToken);
    }
}
