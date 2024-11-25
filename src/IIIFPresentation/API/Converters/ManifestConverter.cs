using API.Helpers;
using Core.Helpers;
using Core.IIIF;
using IIIF;
using IIIF.Presentation;
using Models.API.Manifest;
using Models.Database.Collections;
using Models.Database.General;

namespace API.Converters;

public static class ManifestConverter
{
    /// <summary>
    /// Update <see cref="PresentationManifest"/> with values from DB record.
    /// </summary>
    /// <param name="iiifManifest">Presentation Manifest to update</param>
    /// <param name="dbManifest">Database Manifest</param>
    /// <param name="urlRoots">Current UrlRoots instance</param>
    /// <param name="pathGenerator">used to generate paths</param>
    /// <param name="hierarchyFactory">
    /// Optional factory to specify <see cref="Hierarchy"/> to use to get Parent and Slug. Defaults to using .Single()
    /// </param>
    /// <returns></returns>
    public static PresentationManifest SetGeneratedFields(this PresentationManifest iiifManifest,
        Manifest dbManifest, IPathGenerator pathGenerator, Func<Manifest, Hierarchy>? hierarchyFactory = null)
    {
        hierarchyFactory ??= manifest => manifest.Hierarchy.ThrowIfNull(nameof(manifest.Hierarchy)).Single();
        
        var hierarchy = hierarchyFactory(dbManifest);
        
        iiifManifest.Id = pathGenerator.GenerateFlatManifestId(dbManifest);
        iiifManifest.FlatId = dbManifest.Id;
        iiifManifest.PublicId = pathGenerator.GenerateHierarchicalId(hierarchy);
        iiifManifest.Created = dbManifest.Created.Floor(DateTimeX.Precision.Second);
        iiifManifest.Modified = dbManifest.Modified.Floor(DateTimeX.Precision.Second);
        iiifManifest.CreatedBy = dbManifest.CreatedBy;
        iiifManifest.ModifiedBy = dbManifest.ModifiedBy;
        iiifManifest.Parent = pathGenerator.GenerateFlatParentId(hierarchy);
        iiifManifest.Slug = hierarchy.Slug;
        iiifManifest.EnsurePresentation3Context();
        iiifManifest.EnsureContext(PresentationJsonLdContext.Context);
        
        return iiifManifest;
    }
}