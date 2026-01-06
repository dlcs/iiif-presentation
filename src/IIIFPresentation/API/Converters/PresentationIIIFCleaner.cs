using System.Reflection;
using Core.Helpers;
using Models.API.Collection;
using Models.API.Manifest;

namespace API.Converters;

public static class PresentationIIIFCleaner
{
    private static readonly Func<PresentationManifest, PresentationManifest> OnlyIIIFManifestFunc;
    private static readonly Func<PresentationCollection, PresentationCollection> OnlyIIIFCollectionFunc;

    static PresentationIIIFCleaner()
    {
        // Manifests
        var iiifManifestProps =
            typeof(IIIF.Presentation.V3.Manifest).GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public |
                                                                BindingFlags.Instance)
                .Where(x => x is { CanRead: true, CanWrite: true })
                .Select(x => (get: x.GetGetMethod().ThrowIfNull(x.Name), set: x.GetSetMethod().ThrowIfNull(x.Name)))
                .ToArray();

        OnlyIIIFManifestFunc = input =>
        {
            var output = new PresentationManifest();

            foreach (var prop in iiifManifestProps)
                prop.set.Invoke(output, [prop.get.Invoke(input, null)]);

            return output;
        };
        
        // Collections
        var iiifCollectionProps =
            typeof(IIIF.Presentation.V3.Collection).GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public |
                                                                BindingFlags.Instance)
                .Where(x => x is { CanRead: true, CanWrite: true })
                .Select(x => (get: x.GetGetMethod().ThrowIfNull(x.Name), set: x.GetSetMethod().ThrowIfNull(x.Name)))
                .ToArray();

        OnlyIIIFCollectionFunc = input =>
        {
            var output = new PresentationCollection();

            foreach (var prop in iiifCollectionProps)
                prop.set.Invoke(output, [prop.get.Invoke(input, null)]);

            return output;
        };
    }

    public static PresentationManifest OnlyIIIFProperties(PresentationManifest presentationManifest)
        => OnlyIIIFManifestFunc(presentationManifest);
    
    public static PresentationCollection OnlyIIIFProperties(PresentationCollection presentationCollection)
        => OnlyIIIFCollectionFunc(presentationCollection);
}
