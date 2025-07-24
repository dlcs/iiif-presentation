using API.Features.Manifest;
using API.Helpers;
using API.Infrastructure.IdGenerator;
using API.Tests.Integration.Infrastructure;
using AWS.Helpers;
using Core.Infrastructure;
using DLCS.API;
using DLCS.Models;
using FakeItEasy;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Repository;
using Repository.Paths;
using Services.Manifests;
using Services.Manifests.AWS;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using Collection = Models.Database.Collections.Collection;

namespace API.Tests.Features.Manifest;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ManifestWriteServiceTests
{
    private readonly ManifestWriteService sut;
    private readonly PresentationContext presentationContext;
    private const int Customer = 1;
    private const int NewlyCreatedSpace = 500;
    
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

        var canvasPaintingMerger = new CanvasPaintingMerger();

        var canvasPaintingResolver = new CanvasPaintingResolver(identityManager, manifestItemsParser,
            manifestPaintedResourceParser, canvasPaintingMerger, new NullLogger<CanvasPaintingResolver>());
        
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

        var parentCollection =
            presentationContext.Collections.First(x => x.CustomerId == Customer && x.Id == RootCollection.Id);

        A.CallTo(() =>
            parentSlugParser.Parse(A<PresentationManifest>._, A<int>._, A<string>._,
                A<CancellationToken>._)).ReturnsLazily(
            (PresentationManifest presentationManifest, int customerId, string data,
                    CancellationToken cancellationToken) =>
                ParsedParentSlugResult<PresentationManifest>.Success(new ParsedParentSlug(parentCollection,
                    presentationManifest.Slug!)));
        
        // Always return Space 500 when call to create space
        A.CallTo(() => dlcsClient.CreateSpace(Customer, A<string>._, A<CancellationToken>._))
            .Returns(new Space { Id = NewlyCreatedSpace, Name = "test" });
    }

    [Fact]
    public async Task Create_SuccessfullyCreatesManifest_WhenMixedItemsAndAssets()
    {
        // Arrange
        dynamic asset = new JObject();

        var (slug, resourceId,  assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        asset.id = assetId;

        var manifest = new PresentationManifest()
        {
            Slug = slug,
            Items =
            [
                ManifestTestCreator.Canvas($"https://base/0/canvases/{canvasId}")
                    .WithImage()
                    .Build()
            ],
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = asset,
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "someCanvasId"
                    }
                }
            ]
        };
        
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);
        
        // Act
        var ingestedManifest = await sut.Create(request, CancellationToken.None);
        
        // Assert
        ingestedManifest.Should().NotBeNull();
    }
}
