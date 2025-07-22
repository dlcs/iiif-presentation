using API.Features.Manifest;
using API.Helpers;
using API.Infrastructure.IdGenerator;
using API.Tests.Integration.Infrastructure;
using AWS.Helpers;
using DLCS.API;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Repository;
using Repository.Paths;
using Services.Manifests;
using Services.Manifests.AWS;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Features.Manifest;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ManifestWriteServiceTests
{
    private readonly ManifestWriteService sut;
    private readonly PresentationContext presentationContext;
    private const int Customer = 1;
    
    public ManifestWriteServiceTests(PresentationContextFixture dbFixture)
    {
        presentationContext = dbFixture.DbContext;

        var typedPathTemplateOptions = Options.Create(PathRewriteOptions.Default);
        
        var idGenerator = A.Fake<IIdGenerator>();
        var identityManager = new IdentityManager(idGenerator, presentationContext, new NullLogger<IdentityManager>());
        var iiifS3Service = A.Fake<IIIIFS3Service>();

        var manifestItemsParser = new ManifestItemsParser(new NullLogger<ManifestItemsParser>());
        
        var pathRewriteParser = new PathRewriteParser(typedPathTemplateOptions, new NullLogger<PathRewriteParser>());
        var manifestPaintedResourceParser = new ManifestPaintedResourceParser(pathRewriteParser, new NullLogger<ManifestPaintedResourceParser>());

        var canvasPaintingResolver = new CanvasPaintingResolver(identityManager, manifestItemsParser,
            manifestPaintedResourceParser, new NullLogger<CanvasPaintingResolver>());
        
        var presentationGenerator =
            new TestPresentationConfigGenerator("https://localhost:5000", PathRewriteOptions.Default);

        var manifestRead = A.Fake<IManifestRead>(); //todo: swap to real?

        var dlcsClient = A.Fake<IDlcsApiClient>();
        var managedResultFinder = new ManagedAssetResultFinder(dlcsClient, presentationContext,
            new NullLogger<ManagedAssetResultFinder>());
        var dlcsManifestCoordinator = new DlcsManifestCoordinator(dlcsClient, presentationContext, managedResultFinder,
            new NullLogger<DlcsManifestCoordinator>());

        var parentSlugParser = A.Fake<IParentSlugParser>();

        var manifestStorageManager = A.Fake<IManifestStorageManager>();

        sut = new ManifestWriteService(presentationContext, identityManager, iiifS3Service, canvasPaintingResolver,
            new TestPathGenerator(presentationGenerator), manifestRead, dlcsManifestCoordinator, parentSlugParser,
            manifestStorageManager, new NullLogger<ManifestWriteService>());
    }
}
