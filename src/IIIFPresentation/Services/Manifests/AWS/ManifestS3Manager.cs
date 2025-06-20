using AWS.Helpers;
using Core.Helpers;
using IIIF.Presentation.V3;
using Models.Database.General;
using Repository.Paths;
using Services.Manifests.Helpers;

namespace Services.Manifests.AWS;

public class ManifestS3Manager(
    IIIIFS3Service iiifS3,
    IPathGenerator pathGenerator,
    IManifestMerger manifestMerger) : IManifestS3Manager
{
    public async Task UpdateManifestInS3(Manifest? namedQueryManifest, Models.Database.Collections.Manifest dbManifest, CancellationToken cancellationToken)
    {
        var manifest = await iiifS3.ReadIIIFFromS3<Manifest>(dbManifest, true, cancellationToken);

        var mergedManifest = manifestMerger.ProcessCanvasPaintings(
            manifest.ThrowIfNull(nameof(manifest), "Manifest was not found in staging location"),
            namedQueryManifest,
            dbManifest.CanvasPaintings);

        await iiifS3.SaveIIIFToS3(mergedManifest, dbManifest, pathGenerator.GenerateFlatManifestId(dbManifest),
            false, cancellationToken);

        await iiifS3.DeleteIIIFFromS3(dbManifest, true);
    }
}

public interface IManifestS3Manager
{
    public Task UpdateManifestInS3(Manifest? namedQueryManifest, Models.Database.Collections.Manifest dbManifest,
        CancellationToken cancellationToken);
}
