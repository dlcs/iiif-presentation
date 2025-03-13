using Models.API.Manifest;

namespace API.Helpers;

public static class PaintedResourceX
{
    public static bool HasAsset(this List<PaintedResource>? paintedResources)
    {
        return paintedResources != null && paintedResources.Any(p => p.Asset != null);
    }
}
