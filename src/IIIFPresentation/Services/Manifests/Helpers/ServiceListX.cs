using Core.Helpers;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;

namespace Services.Manifests.Helpers;

public static class ServiceListX
{
    public static (int width, int height)? GetItemDimensionsFromServices(this IList<IService>? services)
    {
        if (services.IsNullOrEmpty())
            return null;

        if (services.OfType<ImageService3>().FirstOrDefault() is { } is3)
            return (is3.Width, is3.Height);

        if (services.OfType<ImageService2>().FirstOrDefault() is { } is2)
            return (is2.Width, is2.Height);

        return null;
    }
}
