using Repository.Paths;

namespace Services.Manifests.Helpers;

public static class CanvasOriginalHelper
{
    public static Uri? TryGetValidCanvasOriginalId(IPresentationPathGenerator presentationPathGenerator, int customerId, 
        string? canvasOriginalId)
    {
        if (string.IsNullOrEmpty(canvasOriginalId)) return null;

        if (Uri.IsWellFormedUriString(canvasOriginalId, UriKind.Absolute))
        {
            return new Uri(canvasOriginalId);
        }
        
        return new Uri(presentationPathGenerator.GetFlatPresentationPathForRequest(PresentationResourceType.Canvas, 
            customerId, canvasOriginalId));
    }
}
