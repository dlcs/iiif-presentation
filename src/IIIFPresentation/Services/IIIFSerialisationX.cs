using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Models.API.Collection;
using Models.API.Manifest;

namespace Services;

public static class IIIFSerialisationX
{
    /// <summary>
    /// Get serialised <see cref="IIIF.Presentation.V3.Manifest"/> from JSON string, removing IIIF Presentation
    /// specific properties
    /// </summary>
    public static Manifest? ToManifest(this string json)
    {
        var manifest = json
            .FromJson<Manifest>()?
            .WithoutAdditionalProperties(PresentationManifest.PresentationPropertyKeys);
        return manifest;
    }

    /// <summary>
    /// Get serialised <see cref="IIIF.Presentation.V3.Collection"/> from JSON string, removing IIIF Presentation
    /// specific properties
    /// </summary>
    public static Collection? ToCollection(this string json)
    {
        var collection = json
            .FromJson<Collection>()?
            .WithoutAdditionalProperties(PresentationCollection.PresentationPropertyKeys);
        return collection;
    }
}
