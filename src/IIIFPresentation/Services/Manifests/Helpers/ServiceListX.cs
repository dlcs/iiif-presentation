using Core.Helpers;
using IIIF;
using IIIF.Auth.V2;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3;

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

    /// <summary>
    /// Recursively walks <paramref name="services"/> (and any services nested within them, e.g. an
    /// ImageService3.Service containing an AuthProbeService2) collecting the ids referenced by any
    /// <see cref="AuthProbeService2"/> found. These reference the 'full' AuthAccessService2 definitions held at
    /// Manifest level.
    /// </summary>
    public static HashSet<string> GetReferencedAuthServiceIds(this IEnumerable<IService>? services)
    {
        var referencedIds = new HashSet<string>();
        CollectReferencedAuthServiceIds(services, referencedIds);
        return referencedIds;
    }

    private static void CollectReferencedAuthServiceIds(IEnumerable<IService>? services, HashSet<string> referencedIds)
    {
        if (services == null) return;

        foreach (var service in services)
        {
            if (service is AuthProbeService2 probeService)
            {
                foreach (var reference in probeService.Service ?? [])
                {
                    if (reference.Id != null) referencedIds.Add(reference.Id);
                }
            }

            if (service is ResourceBase { Service: { } nestedServices })
            {
                CollectReferencedAuthServiceIds(nestedServices, referencedIds);
            }
        }
    }
}
