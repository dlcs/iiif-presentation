using API.Features.Manifest;
using API.Helpers;
using API.Infrastructure.IdGenerator;
using API.Tests.Integration.Infrastructure;
using AWS.Helpers;
using DLCS;
using DLCS.API;
using DLCS.Models;
using FakeItEasy;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Newtonsoft.Json.Linq;
using Repository;
using Repository.Paths;
using Services.Manifests;
using Services.Manifests.AWS;
using Services.Manifests.Helpers;
using Services.Manifests.Settings;
using Sqids;
using Test.Helpers;
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
    private const int NewlyCreatedSpace = 500;
    
    public ManifestWriteServiceTests(PresentationContextFixture dbFixture)
    {
        presentationContext = dbFixture.DbContext;

        var typedPathTemplateOptions = Options.Create(PathRewriteOptions.Default);
        
        var sqidsEncoder = new SqidsEncoder<long>();
        var idGenerator = new SqidsGenerator(sqidsEncoder, new NullLogger<SqidsGenerator>());
        
        var identityManager = new IdentityManager(idGenerator, presentationContext, new NullLogger<IdentityManager>());
        var iiifS3Service = A.Fake<IIIIFS3Service>();
        
        var presentationGenerator =
            new TestPresentationConfigGenerator("https://localhost:5000", PathRewriteOptions.Default);
        
        var pathRewriteParser = new PathRewriteParser(typedPathTemplateOptions, new NullLogger<PathRewriteParser>());

        var manifestItemsParser = new ManifestItemsParser(pathRewriteParser, presentationGenerator,
            Options.Create(new PathSettings(){PresentationApiUrl = new Uri("https://base")}), new NullLogger<ManifestItemsParser>());
        
        var manifestPaintedResourceParser = new ManifestPaintedResourceParser(pathRewriteParser, presentationGenerator,
            new NullLogger<ManifestPaintedResourceParser>());

        var canvasPaintingMerger = new CanvasPaintingMerger(pathRewriteParser);

        var canvasPaintingResolver = new CanvasPaintingResolver(identityManager, manifestItemsParser,
            manifestPaintedResourceParser, canvasPaintingMerger, new NullLogger<CanvasPaintingResolver>());

        var dlcsClient = A.Fake<IDlcsApiClient>();
        var managedResultFinder = new ManagedAssetResultFinder(dlcsClient, presentationContext,
            new NullLogger<ManagedAssetResultFinder>());
        var dlcsManifestCoordinator = new DlcsManifestCoordinator(dlcsClient, presentationContext, managedResultFinder,
            new NullLogger<DlcsManifestCoordinator>());

        var parentSlugParser = A.Fake<IParentSlugParser>();

        var manifestStorageManager = A.Fake<IManifestStorageManager>();
        var settingsBasedPathGenerator = new SettingsBasedPathGenerator(Options.Create(new DlcsSettings
        {
            ApiUri = new Uri("https://dlcs.api")
        }), new SettingsDrivenPresentationConfigGenerator(Options.Create(new PathSettings()
        {
            PresentationApiUrl = new Uri("https://presentation.api"),
            PathRules = PathRewriteOptions.Default
        })));

        sut = new ManifestWriteService(presentationContext, identityManager, iiifS3Service, canvasPaintingResolver,
            new TestPathGenerator(presentationGenerator), settingsBasedPathGenerator, dlcsManifestCoordinator, parentSlugParser,
            manifestStorageManager, pathRewriteParser, new NullLogger<ManifestWriteService>());

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
                        CanvasId = "someCanvasId",
                        CanvasOrder = 1
                    }
                }
            ]
        };
        
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);
        
        // Act
        var ingestedManifest = await sut.Create(request, CancellationToken.None);
        
        // Assert
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should().BeNull();
        ingestedManifest.Entity.PaintedResources.Should().HaveCount(2);
        
        var dbManifest = presentationContext.Manifests.Include(m => m.CanvasPaintings)
            .First(x => x.Id == ingestedManifest.Entity.FlatId);
        dbManifest.CanvasPaintings.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task Create_FailsToCreateManifest_WhenCanvasIdNotMatched()
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
                ManifestTestCreator.Canvas($"https://base/0/canvases/additionalSlug/{canvasId}")
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
                        CanvasId = canvasId
                    }
                }
            ]
        };
        
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);
        
        // Act
        var ingestedManifest = await sut.Create(request, CancellationToken.None);
        
        // Assert
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should().Be($"Canvas painting records with the following id's conflict with the order from items - {canvasId}");
    }
    
    [Fact]
    public async Task Create_ReturnsError_WhenMixedItemsAndAssetsWithErrors()
    {
        // Arrange
        dynamic asset = new JObject();
        var (slug, resourceId,  assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        asset.id = assetId;
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = $"https://base/0/canvases/{canvasId}",
                    Label = new LanguageMap("some", "label")
                }
            ],
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = asset,
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = 20,
                        CanvasLabel = new LanguageMap("some", "different label")
                    }
                }
            ]
        };
        
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);
        
        // Act
        var ingestedManifest = await sut.Create(request, CancellationToken.None);
        
        // Assert
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should().Be($"Canvas painting with id {canvasId} does not have a matching canvas label");
    }
    
    [Fact]
    public async Task Create_SuccessfullyCreatesManifest_WhenShortFormCanvasOriginalIdMatchesPaintedResource()
    {
        // Arrange
        dynamic asset = new JObject();

        var (slug, resourceId,  assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        asset.id = assetId;

        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas()
                {
                    Id = canvasId
                }
            ],
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = asset,
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId
                    }
                }
            ]
        };
        
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);
        
        // Act
        var ingestedManifest = await sut.Create(request, CancellationToken.None);
        
        // Assert
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should().BeNull();
        ingestedManifest.Entity.PaintedResources.Should().HaveCount(1);

        var dbManifest = presentationContext.Manifests.Include(m => m.CanvasPaintings)
            .First(x => x.Id == ingestedManifest.Entity.FlatId);
        dbManifest.CanvasPaintings.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task Create_ThrowsError_WhenShortCanvasIdUsedWithoutMatchingPaintedResource()
    {
        // Arrange
        dynamic asset = new JObject();

        var (slug, resourceId,  assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();

        asset.id = assetId;

        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "shortCanvas",
                    Items =
                    [
                        new AnnotationPage
                        {
                            Id = "shortCanvas/annopages/0",
                            Items = 
                            [
                                new PaintingAnnotation
                                {
                                    Id = "shortCanvas/annotations/0",
                                    Target = new Canvas { Id = "shortCanvas" },
                                    Body = new Image
                                    {
                                        Id = "shortCanvas/annotations/0/image.png",
                                        Width = 100,
                                        Height = 100
                                    }
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);

        // Act
        var ingestedManifest = await sut.Create(request, CancellationToken.None);

        // Assert
        // Assert
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should().Be("The canvas id shortCanvas is invalid - The canvas id is not a valid URI, and cannot be matched with a painted resource");
    }

    [Fact]
    public async Task Create_SuccessfullyCreatesManifest_WhenShortCanvasIdUsedWithMatchingCanvasId()
    {
        // Arrange
        dynamic asset = new JObject();

        var (slug, resourceId,  assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();

        asset.id = assetId;
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "shortCanvas"
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "shortCanvas"
                    },
                    Asset = asset
                }
            ]
        };

        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);

        // Act
        var ingestedManifest = await sut.Create(request, CancellationToken.None);

        // Assert
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should().BeNull();
        ingestedManifest.Entity.PaintedResources.Should().HaveCount(1);
        ingestedManifest.Entity.Items.First().Id.Should().Be("https://presentation.api/1/canvases/shortCanvas");
        var paintedResource = ingestedManifest.Entity.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/shortCanvas");
        paintedResource.CanvasPainting.CanvasOriginalId.Should().BeNull();
    }
}
