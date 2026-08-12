using System.Reflection;
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
    private static readonly PropertyInfo[] ManifestProperties = GetCopyableProperties(typeof(Manifest));
    private static readonly PropertyInfo[] CollectionProperties = GetCopyableProperties(typeof(Collection));

    public static Manifest ToManifest(this PresentationManifest presentationManifest)
    {
        var manifest = new Manifest();
        CopyProperties(presentationManifest, manifest, ManifestProperties);
        return manifest;
    }

    public static Collection ToCollection(this PresentationCollection presentationCollection)
    {
        var collection = new Collection();
        CopyProperties(presentationCollection, collection, CollectionProperties);
        return collection;
    }

    private static PropertyInfo[] GetCopyableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
            .ToArray();

    private static void CopyProperties(object source, object target, PropertyInfo[] properties)
    {
        foreach (var property in properties)
        {
            property.SetValue(target, property.GetValue(source));
        }
    }
}
