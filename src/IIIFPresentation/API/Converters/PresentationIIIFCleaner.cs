using IIIF.Presentation.V3;
using Models.API.Collection;
using Models.API.Manifest;

namespace API.Converters;

/// <summary>
/// Hierarchical write responses are always plain IIIF, regardless of headers. This strips Presentation-only
/// properties (e.g. Slug, Parent, PublicId) off a Presentation* model, copying only the base IIIF properties across
/// to a fresh instance of the base type.
/// </summary>
public static class PresentationIIIFCleaner
{
    public static Manifest ToManifest(this PresentationManifest presentationManifest)
    {
        var manifest = new Manifest();
        IIIFPropertyCopier.CopyProperties<Manifest>(presentationManifest, manifest);
        return manifest;
    }

    public static Collection ToCollection(this PresentationCollection presentationCollection)
    {
        var collection = new Collection();
        IIIFPropertyCopier.CopyProperties<Collection>(presentationCollection, collection);
        return collection;
    }
}
