using API.Helpers;
using Core.Helpers;
using Core.IIIF;
using IIIF;
using IIIF.Presentation;
using Models.API.Manifest;

namespace API.Converters;

public static class ManifestConverter
{
    public static PresentationManifest SetGeneratedFields(this PresentationManifest iiifManifest,
        Models.Database.Collections.Manifest dbManifest, UrlRoots urlRoots)
    {
        iiifManifest.Id = dbManifest.GenerateFlatManifestId(urlRoots);
        iiifManifest.Created = dbManifest.Created.Floor(DateTimeX.Precision.Second);
        iiifManifest.Modified = dbManifest.Modified.Floor(DateTimeX.Precision.Second);
        iiifManifest.CreatedBy = dbManifest.CreatedBy;
        iiifManifest.ModifiedBy = dbManifest.ModifiedBy;
        iiifManifest.EnsurePresentation3Context();
        iiifManifest.EnsureContext(PresentationJsonLdContext.Context);
        
        return iiifManifest;
    }
}