using API.Features.Manifest;
using API.Helpers;
using API.Infrastructure;
using API.Infrastructure.IdGenerator;
using API.Settings;
using API.Tests.Integration.Infrastructure;
using AWS.Settings;
using Core;
using DLCS;
using DLCS.API;
using DLCS.Models;
using FakeItEasy;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Models.DLCS;
using DbBatchStatus = Models.Database.General.BatchStatus;
using DbDeliverableType = Models.Database.General.DeliverableType;
using Models.Database.General;
using Newtonsoft.Json.Linq;
using Repository;
using Repository.Paths;
using Services.Manifests;
using Services.Manifests.AWS;
using Services.Manifests.Helpers;
using Services.Manifests.Settings;
using Services.TextServices;
using Sqids;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using Test.Helpers.Settings;
using DbCanvasPainting = Models.Database.CanvasPainting;
using DbManifest = Models.Database.Collections.Manifest;
using IIIFManifest = IIIF.Presentation.V3.Manifest;

namespace API.Tests.Features.Manifest;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ManifestWriteServiceTests
{
    private readonly ManifestWriteService sut;
    private readonly PresentationContext presentationContext;
    private const int Customer = 1;
    private const int NewlyCreatedSpace = 500;
    private readonly DlcsSettings dlcsSettings;
    private readonly IDlcsApiClient dlcsClient;
    private readonly IManifestStorageManager manifestStorageManager;
    private readonly LockManager manifestLockManager;
    private readonly ITextServicesClient textServicesClient;
    
    public ManifestWriteServiceTests(PresentationContextFixture dbFixture)
    {
        presentationContext = dbFixture.DbContext;
        dbFixture.CustomerIdProvider.SetCustomerId(Customer);
        
        dlcsSettings = DefaultSettings.DlcsSettings();

        var typedPathTemplateOptions = Options.Create(PathRewriteOptions.Default);
        
        var sqidsEncoder = new SqidsEncoder<long>();
        var idGenerator = new SqidsGenerator(sqidsEncoder, new NullLogger<SqidsGenerator>());
        
        var identityManager = new IdentityManager(idGenerator, presentationContext, new NullLogger<IdentityManager>());
        
        var presentationGenerator =
            new TestPresentationConfigGenerator("https://localhost:5000", PathRewriteOptions.Default);
        
        var pathRewriteParser = new PathRewriteParser(typedPathTemplateOptions, new NullLogger<PathRewriteParser>());

        var pathSettings = new PathSettings { PresentationApiUrl = new Uri("https://base") };

        var canvasHelper = new CanvasHelper(Options.Create(new ServicesSettings()));

        var manifestItemsParser = new ManifestItemsParser(pathRewriteParser, presentationGenerator,
            new PaintableAssetIdentifier(OptionsHelpers.GetOptionsMonitor(dlcsSettings),
                new NullLogger<PaintableAssetIdentifier>()), Options.Create(pathSettings), canvasHelper,
            new NullLogger<ManifestItemsParser>());

        var manifestPaintedResourceParser = new ManifestPaintedResourceParser(pathRewriteParser, presentationGenerator,
            Options.Create(pathSettings), presentationContext, canvasHelper, new NullLogger<ManifestPaintedResourceParser>());

        var canvasPaintingMerger = new CanvasPaintingMerger(pathRewriteParser);

        var canvasPaintingResolver = new CanvasPaintingResolver(identityManager, manifestItemsParser,
            manifestPaintedResourceParser, canvasPaintingMerger, new NullLogger<CanvasPaintingResolver>());
        
        dlcsClient = A.Fake<IDlcsApiClient>();
        
        var apiOptions = Options.Create(new ApiSettings()
        {
            AWS = new AWSSettings(),
            DLCS = dlcsSettings
        });
            
        var managedResultFinder = new ManagedAssetResultFinder(dlcsClient, presentationContext, apiOptions,
            new NullLogger<ManagedAssetResultFinder>());
        var dlcsManifestCoordinator = new DlcsManifestCoordinator(dlcsClient, presentationContext, managedResultFinder,
            new NullLogger<DlcsManifestCoordinator>());

        var parentSlugParser = A.Fake<IParentSlugParser>();

        manifestStorageManager = A.Fake<IManifestStorageManager>();
        var settingsBasedPathGenerator = new SettingsBasedPathGenerator(Options.Create(dlcsSettings),
            new SettingsDrivenPresentationConfigGenerator(Options.Create(new PathSettings()
        {
            PresentationApiUrl = new Uri("https://presentation.api"),
            PathRules = PathRewriteOptions.Default
        })));

        manifestLockManager = new LockManager();

        textServicesClient = A.Fake<ITextServicesClient>();
        A.CallTo(() => textServicesClient.CreateOrUpdateJob(A<PipelineJob>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(true);
        sut = new ManifestWriteService(presentationContext, identityManager, canvasPaintingResolver,
            new TestPathGenerator(presentationGenerator), settingsBasedPathGenerator, dlcsManifestCoordinator, parentSlugParser,
            manifestStorageManager, pathRewriteParser, manifestLockManager, textServicesClient,
            Options.Create(new AWSSettings()), new NullLogger<ManifestWriteService>());

        var parentCollection =
            presentationContext.Collections.First(x => x.Id == RootCollection.Id);

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
                        CanvasId = TestIdentifiers.IdCanvasPainting().canvasPaintingId,
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

        // Saved to staging with an original payload stored - must not attempt to delete any original payload
        A.CallTo(() => manifestStorageManager.DeleteOriginalPayload(
            A<Models.Database.Collections.Manifest>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Create_DeletesStaleOriginalPayload_WhenSavedDirectlyWithoutExternalContent()
    {
        // Arrange - a plain manifest with no assets or adjuncts is built upfront and saved directly to S3
        var (slug, resourceId) = TestIdentifiers.SlugResource();

        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items = [new Canvas { Id = "https://base/0/canvases/canvas-1" }]
        };

        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);

        // Act
        var ingestedManifest = await sut.Create(request, CancellationToken.None);

        // Assert
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should().BeNull();
        A.CallTo(() => manifestStorageManager.DeleteOriginalPayload(
            A<Models.Database.Collections.Manifest>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_RecognizesDlcsAsset_WhenMixedItemsAndAssets()
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
        
        // update the single image in items
        const int managedImageSpace = 33;
        const string managedImageAssetName = "theAssetId";
        var paintingAnnotation = (PaintingAnnotation)manifest.Items![0].Items![0].Items![0];
        var image = (Image)paintingAnnotation.Body!;
        image.Id = $"{dlcsSettings.OrchestratorUri}/iiif-img/{Customer}/{managedImageSpace}/{managedImageAssetName}/full/max/0/default.jpg";
        
        // set the DLCS fake to recognize the image
        var imageAssetId = new AssetId(Customer,managedImageSpace,managedImageAssetName);

        A.CallTo(()=>dlcsClient.GetCustomerImages(Customer,
            A<IList<string>>.That.Matches(l=>l.Any(x => imageAssetId.ToString().Equals(x))), A<CancellationToken>._))
            .ReturnsLazily(() =>
            [
                JObject.Parse($$"""
                                {
                                    "@id": "https://localhost:6000/customers/{{Customer}}/spaces/{{managedImageSpace}}/images/{{managedImageAssetName}}",
                                    "id": "{{managedImageAssetName}}",
                                    "space": {{managedImageSpace}},
                                    "batch": "https://localhost/customers/1/queue/batches/2137"
                                }
                                """
                )
            ]);
        
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
        dbManifest.CanvasPaintings.Count(cp=>cp.AssetId.Equals(imageAssetId)).Should().Be(1);

        // Call made to update assets with manifest ids where they are assets from items
        A.CallTo(() => dlcsClient.UpdateAssetManifest(A<int>._,
            A<ICollection<string>>.That.Matches(l => l.First() == imageAssetId.ToString()), OperationType.Add,
            A<List<string>>._, A<CancellationToken>._)).MustHaveHappened();
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
        ingestedManifest.Error.Should().Be($"The following canvas painting records conflict with the order from items - (id: {canvasId}, canvasOrder: 0)");
    }
    
    [Fact]
    public async Task Create_FailsToCreateManifest_WhenBlankCanvasIdNotMatched()
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
                        CanvasId = null
                    }
                }
            ]
        };
        
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);
        
        // Act
        var ingestedManifest = await sut.Create(request, CancellationToken.None);
        
        // Assert
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should()
            .Be(
                "The following canvas painting records conflict with the order from items - (canvasOrder: 0)");
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

        var (slug, resourceId,  assetId) = TestIdentifiers.SlugResourceAsset();

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

        // Setup a fake batch with resource ID, this is unfinished so means it's sync complete
        A.CallTo(() => dlcsClient.IngestDeliverables(Customer, A<List<JObject>>._, false, A<CancellationToken>._))
            .Returns([new DLCS.Models.Batch { Finished = null, ResourceId = "12345" }]);

        var (slug, resourceId, assetId) = TestIdentifiers.SlugResourceAsset();

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
        ingestedManifest.Entity!.PaintedResources.Should().HaveCount(1);
        ingestedManifest.Entity.Items!.First().Id.Should().Be("https://presentation.api/1/canvases/shortCanvas");
        var paintedResource = ingestedManifest.Entity.PaintedResources!.First();
        paintedResource.CanvasPainting!.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/shortCanvas");
        paintedResource.CanvasPainting.CanvasOriginalId.Should().BeNull();
    }
    
    [Fact]
    public async Task Create_ErrorCreatingManifest_WhenErrorWithPaintableAsset()
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
                    Id = "https://test.com/item",
                    Items =
                    [
                        new AnnotationPage
                        {
                            Items =
                            [
                                new PaintingAnnotation
                                {
                                    Body = new Image
                                    {
                                        Id = $"https://dlcs.orchestrator/iiif-img/{Customer}/1/someItem",
                                        Service = 
                                        [
                                            new ImageService3
                                            {
                                                Id =  $"https://dlcs.orchestrator/iiif-img/{Customer}/1/different",
                                            }
                                        ]
                                    },
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
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should().Be("Suspected asset from image body (1/1/someItem) and services (1/1/different) point to different managed assets");
    }
    
    [Fact]
    public async Task Upsert_MergesStubAdjuncts_WithUserSetValues_WhenCanBeBuiltUpfront()
    {
        // Arrange - manifest with a tracked asset so canBeBuiltUpfront = true (no new batches needed)
        var (slug, resourceId, assetId) = TestIdentifiers.SlugResourceAsset();
        const string userSeeAlsoId = "https://example.com/user-see-also.xml";
        const string stubSeeAlsoId = "https://example.com/stub-see-also.xml";
        const string userRenderingId = "https://example.com/user-rendering.pdf";
        const string stubRenderingId = "https://example.com/stub-rendering.pdf";
        const string userAnnotationId = "https://example.com/user-annotations";
        const string stubAnnotationId = "https://example.com/stub-annotations";

        dynamic asset = new JObject();
        asset.id = assetId;

        var canvasPainting = new DbCanvasPainting
        {
            Id = "cp1",
            CustomerId = Customer,
            CanvasOrder = 1,
            ChoiceOrder = 1,
            AssetId = new AssetId(Customer, NewlyCreatedSpace, assetId)
        };

        var dbManifest = await presentationContext.Manifests.AddTestManifest(resourceId, slug: slug,
            canvasPaintings: [canvasPainting], spaceId: NewlyCreatedSpace);
        await presentationContext.SaveChangesAsync();

        // UpsertManifestInStorage returns a manifest carrying both user-set and stub values for all
        // three adjunct types, as ManifestMerger would produce after applying ApplyManifestLevelAdjuncts
        A.CallTo(() => manifestStorageManager.UpsertManifestInStorage(
                A<IIIFManifest>._, A<Models.Database.Collections.Manifest>._, A<string>._, A<bool>._, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest
            {
                SeeAlso =
                [
                    new ExternalResource("SeeAlso") { Id = userSeeAlsoId },
                    new ExternalResource("SeeAlso") { Id = stubSeeAlsoId }
                ],
                Rendering =
                [
                    new ExternalResource("Rendering") { Id = userRenderingId },
                    new ExternalResource("Rendering") { Id = stubRenderingId }
                ],
                Annotations =
                [
                    new AnnotationPage { Id = userAnnotationId },
                    new AnnotationPage { Id = stubAnnotationId }
                ]
            });

        const string stubManifestAdjunctId = "manifest-adjunct.xml";
        var stubAssetName = $"Manifest_{resourceId}";
        A.CallTo(() => dlcsClient.GetCustomerImages(Customer, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IList<JObject>>(
            [
                JObject.Parse($$"""
                {
                    "@id": "https://localhost/customers/{{Customer}}/spaces/0/images/{{stubAssetName}}",
                    "id": "{{stubAssetName}}",
                    "space": 0,
                    "adjuncts": [{ "id": "{{stubManifestAdjunctId}}", "mediaType": "text/xml" }]
                }
                """)
            ]));

        var manifest = new PresentationManifest
        {
            Slug = slug,
            SeeAlso = [new ExternalResource("SeeAlso") { Id = userSeeAlsoId }],
            Rendering = [new ExternalResource("Rendering") { Id = userRenderingId }],
            Annotations = [new AnnotationPage { Id = userAnnotationId }],
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = asset,
                    CanvasPainting = new CanvasPainting { CanvasOrder = 1 }
                }
            ]
        };

        var request = new UpsertManifestRequest(resourceId, dbManifest.Entity.Etag.ToString(), Customer, manifest,
            manifest.AsJson(), true);

        // Act
        var result = await sut.Upsert(request, CancellationToken.None);

        // Assert
        result.Error.Should().BeNull();
        result.Entity.SeeAlso.Should()
            .Contain(s => s.Id == userSeeAlsoId, "user-set seeAlso must not be lost when stub canvas adjuncts are merged").And
            .Contain(s => s.Id == stubSeeAlsoId, "stub canvas seeAlso must be added alongside user-set values");
        result.Entity.Rendering.Should()
            .Contain(r => r.Id == userRenderingId, "user-set rendering must not be lost when stub canvas adjuncts are merged").And
            .Contain(r => r.Id == stubRenderingId, "stub canvas rendering must be added alongside user-set values");
        result.Entity.Annotations.Should()
            .Contain(a => a.Id == userAnnotationId, "user-set annotations must not be lost when stub canvas adjuncts are merged").And
            .Contain(a => a.Id == stubAnnotationId, "stub canvas annotations must be added alongside user-set values");
        result.Entity.Adjuncts.Should().ContainSingle()
            .Which.Value<string>("id").Should().Be(stubManifestAdjunctId,
                "manifest-level adjuncts from the DLCS stub asset must be returned when Adjuncts was null on the request");
    }

    [Fact]
    public async Task Upsert_PreservesEmptyAdjuncts_WhenStubCanvasHasNoAdjuncts()
    {
        // Arrange - manifest with a tracked asset so canBeBuiltUpfront = true
        var (slug, resourceId, assetId) = TestIdentifiers.SlugResourceAsset();

        dynamic asset = new JObject();
        asset.id = assetId;

        var canvasPainting = new DbCanvasPainting
        {
            Id = "cp1",
            CustomerId = Customer,
            CanvasOrder = 1,
            ChoiceOrder = 1,
            AssetId = new AssetId(Customer, NewlyCreatedSpace, assetId)
        };

        var dbManifest = await presentationContext.Manifests.AddTestManifest(resourceId, slug: slug,
            canvasPaintings: [canvasPainting], spaceId: NewlyCreatedSpace);
        await presentationContext.SaveChangesAsync();

        // ManifestMerger preserves the base manifest's empty lists when the stub canvas has no adjuncts,
        // so UpsertManifestInStorage returns empty (not null) for each adjunct type
        A.CallTo(() => manifestStorageManager.UpsertManifestInStorage(
                A<IIIFManifest>._, A<Models.Database.Collections.Manifest>._, A<string>._, A<bool>._, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest { SeeAlso = [], Rendering = [], Annotations = [] });

        A.CallTo(() => dlcsClient.GetCustomerImages(Customer, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IList<JObject>>([]));

        var manifest = new PresentationManifest
        {
            Slug = slug,
            SeeAlso = [],
            Rendering = [],
            Annotations = [],
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = asset,
                    CanvasPainting = new CanvasPainting { CanvasOrder = 1 }
                }
            ]
        };

        var request = new UpsertManifestRequest(resourceId, dbManifest.Entity.Etag.ToString(), Customer, manifest,
            manifest.AsJson(), true);

        // Act
        var result = await sut.Upsert(request, CancellationToken.None);

        // Assert: empty lists from the base manifest are passed through unchanged
        result.Error.Should().BeNull();
        result.Entity.SeeAlso.Should().BeEmpty();
        result.Entity.Rendering.Should().BeEmpty();
        result.Entity.Annotations.Should().BeEmpty();
        result.Entity.Adjuncts.Should().BeNull("no stub asset carries adjuncts so Adjuncts should not be populated");
    }

    [Fact]
    public async Task Upsert_CallsUpsertManifestInStorage_WhenAdjunctsNull_AndExistingAdjunctBatch()
    {
        // Arrange - no painted resources, adjuncts = null ("no change"), but the manifest has a previously
        // ingested adjunct batch. ManifestMerger must run to bake the existing DLCS adjunct properties
        // (SeeAlso/Rendering/Annotations) back into the S3 manifest.
        var (slug, resourceId) = TestIdentifiers.SlugResource();

        var dbManifest = await presentationContext.Manifests.AddTestManifest(resourceId, slug: slug);
        await presentationContext.Batches.AddTestBatch(9991, dbManifest.Entity, DbDeliverableType.Adjunct, DbBatchStatus.Completed);
        await presentationContext.SaveChangesAsync();

        A.CallTo(() => manifestStorageManager.UpsertManifestInStorage(
                A<IIIFManifest>._, A<Models.Database.Collections.Manifest>._, A<string>._, A<bool>._, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest());

        const string existingAdjunctId = "existing-adjunct.xml";
        var stubAssetName = $"Manifest_{resourceId}";
        A.CallTo(() => dlcsClient.GetCustomerImages(Customer, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IList<JObject>>(
            [
                JObject.Parse($$"""
                {
                    "@id": "https://localhost/customers/{{Customer}}/spaces/0/images/{{stubAssetName}}",
                    "id": "{{stubAssetName}}",
                    "space": 0,
                    "adjuncts": [{ "id": "{{existingAdjunctId}}", "mediaType": "text/xml" }]
                }
                """)
            ]));

        var manifest = new PresentationManifest { Slug = slug, Adjuncts = null };

        var request = new UpsertManifestRequest(resourceId, dbManifest.Entity.Etag.ToString(), Customer, manifest,
            manifest.AsJson(), false);

        // Act
        var result = await sut.Upsert(request, CancellationToken.None);

        // Assert
        result.Error.Should().BeNull();
        result.Entity.Adjuncts.Should().ContainSingle()
            .Which.Value<string>("id").Should().Be(existingAdjunctId,
                "existing adjuncts from the DLCS stub asset must be returned when Adjuncts was null on the request");
        A.CallTo(() => manifestStorageManager.UpsertManifestInStorage(
                A<IIIFManifest>._, A<Models.Database.Collections.Manifest>._, A<string>._, A<bool>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Upsert_CallsSaveManifestInStorage_WhenAdjunctsNull_AndNoPriorDlcsContent()
    {
        // Arrange - items-only manifest: no painted resources, no adjuncts, no prior DLCS batches.
        // ManifestMerger must NOT be called — doing so would make an unnecessary DLCS NQ call.
        var (slug, resourceId) = TestIdentifiers.SlugResource();

        var dbManifest = await presentationContext.Manifests.AddTestManifest(resourceId, slug: slug);
        await presentationContext.SaveChangesAsync();

        A.CallTo(() => dlcsClient.GetCustomerImages(Customer, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IList<JObject>>([]));

        var manifest = new PresentationManifest { Slug = slug, Adjuncts = null };

        var request = new UpsertManifestRequest(resourceId, dbManifest.Entity.Etag.ToString(), Customer, manifest,
            manifest.AsJson(), false);

        // Act
        var result = await sut.Upsert(request, CancellationToken.None);

        // Assert
        result.Error.Should().BeNull();
        result.Entity.Adjuncts.Should().BeNull("no DLCS content exists so the stub asset has no adjuncts to return");
        A.CallTo(() => manifestStorageManager.UpsertManifestInStorage(
                A<IIIFManifest>._, A<Models.Database.Collections.Manifest>._, A<string>._, A<bool>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Upsert_CallsUpsertManifestInStorage_WhenAdjunctsEmpty_AndNoAssets()
    {
        // Arrange - no painted resources, adjuncts = [] (explicit clear) and stub asset already in DLCS.
        // canBeBuiltUpfront = true (stub exists, no new batch needed) so ManifestMerger must be called.
        var (slug, resourceId) = TestIdentifiers.SlugResource();

        var dbManifest = await presentationContext.Manifests.AddTestManifest(resourceId, slug: slug);
        await presentationContext.SaveChangesAsync();

        // Return the existing stub when DLCS is queried for it, simulating the stub already existing
        var stubAssetName = $"Manifest_{resourceId}";
        var stubAssetId = $"{Customer}/0/{stubAssetName}";
        A.CallTo(() => dlcsClient.GetCustomerImages(Customer,
                A<IList<string>>.That.Matches(l => l.Contains(stubAssetId)), A<CancellationToken>._))
            .Returns(Task.FromResult<IList<JObject>>(
            [
                JObject.Parse($$"""{ "id": "{{stubAssetName}}", "space": 0 }""")
            ]));

        A.CallTo(() => manifestStorageManager.UpsertManifestInStorage(
                A<IIIFManifest>._, A<Models.Database.Collections.Manifest>._, A<string>._, A<bool>._, A<CancellationToken>._))
            .ReturnsLazily(() => new IIIFManifest());

        var manifest = new PresentationManifest { Slug = slug, Adjuncts = [] };

        var request = new UpsertManifestRequest(resourceId, dbManifest.Entity.Etag.ToString(), Customer, manifest,
            manifest.AsJson(), false);

        // Act
        var result = await sut.Upsert(request, CancellationToken.None);

        // Assert
        result.Error.Should().BeNull();
        result.Entity.Adjuncts.Should().BeEmpty("Adjuncts=[] (explicit clear) is preserved — stub lookup finds no adjuncts so the value is unchanged");
        A.CallTo(() => manifestStorageManager.UpsertManifestInStorage(
                A<IIIFManifest>._, A<Models.Database.Collections.Manifest>._, A<string>._, A<bool>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Upsert_ErrorCreatingManifest_WhenErrorWithPaintableAsset()
    {
        // Arrange
        dynamic asset = new JObject();

        var (slug, resourceId,  assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();

        var dbManifest = await presentationContext.Manifests.AddTestManifest(resourceId);
        await presentationContext.SaveChangesAsync();

        asset.id = assetId;
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "https://test.com/item",
                    Items =
                    [
                        new AnnotationPage
                        {
                            Items =
                            [
                                new PaintingAnnotation
                                {
                                    Body = new Image
                                    {
                                        Id = $"https://dlcs.orchestrator/iiif-img/{Customer}/1/someItem",
                                        Service = 
                                        [
                                            new ImageService3
                                            {
                                                Id =  $"https://dlcs.orchestrator/iiif-img/{Customer}/1/different",
                                            }
                                        ]
                                    },
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var request = new UpsertManifestRequest(resourceId, dbManifest.Entity.Etag.ToString(), Customer, manifest, manifest.AsJson(), true);

        // Act
        var ingestedManifest = await sut.Upsert(request, CancellationToken.None);

        // Assert
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should().Be("Suspected asset from image body (1/1/someItem) and services (1/1/different) point to different managed assets");
        ingestedManifest.WriteResult.Should().Be(WriteResult.BadRequest);
    }

    [Fact]
    public async Task Upsert_ReturnsConflict_WhenManifestIsAlreadyBeingProcessed()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();

        // Hold the lock externally to simulate another in-flight request
        using var heldLock = manifestLockManager.TryAcquire($"M:{Customer}:{resourceId}");

        var manifest = new PresentationManifest { Slug = slug };
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), false);

        // Act
        var result = await sut.Upsert(request, CancellationToken.None);

        // Assert
        result.WriteResult.Should().Be(WriteResult.Conflict);
        result.Error.Should().Contain("currently being");
    }

    [Fact]
    public async Task Create_ErrorCreatingManifest_WhenCannotFindAssetFromItemsInDlcs()
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
                    Id = "https://test.com/item",
                    Items =
                    [
                        new AnnotationPage
                        {
                            Items =
                            [
                                new PaintingAnnotation
                                {
                                    Body = new Image
                                    {
                                        Id = $"https://dlcs.orchestrator/iiif-img/{Customer}/1/someItem",
                                        Service = 
                                        [
                                            new ImageService3
                                            {
                                                Id =  $"https://dlcs.orchestrator/iiif-img/{Customer}/1/someItem",
                                            }
                                        ]
                                    },
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
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should().Be($"Suspected DLCS assets from items not found: (id: https://test.com/item, assetId: {Customer}/1/someItem)");
    }
    
    [Fact]
    public async Task Create_SuccessfullyCreatesManifest_WhenItemOnlyFollowedByMatched()
    {
        // Arrange
        dynamic asset = new JObject();
        dynamic assetTwo = new JObject();

        var (slug, resourceId,  assetId, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        asset.id = $"{assetId}_1";
        assetTwo.id = $"{assetId}_2";

        var manifest = new PresentationManifest()
        {
            Slug = slug,
            Items =
            [
                ManifestTestCreator.Canvas($"https://base/0/canvases/{canvasId}_1")
                    .WithImage()
                    .Build(),
                new Canvas
                {
                    Id = $"https://base/0/canvases/{canvasId}_2",
                },
                ManifestTestCreator.Canvas($"https://base/0/canvases/{canvasId}_3")
                    .WithImage()
                    .Build(),
                new Canvas
                {
                    Id = $"https://base/0/canvases/{canvasId}_4",
                },
            ],
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = asset,
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = $"{canvasId}_2",
                        CanvasOrder = 1
                    }
                },
                new PaintedResource
                {
                    Asset = assetTwo,
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = $"{canvasId}_4",
                        CanvasOrder = 3
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
        ingestedManifest.Entity.PaintedResources.Should().HaveCount(4);
        
        var dbManifest = presentationContext.Manifests.Include(m => m.CanvasPaintings)
            .First(x => x.Id == ingestedManifest.Entity.FlatId);
        dbManifest.CanvasPaintings.Should().HaveCount(4);
        dbManifest.CanvasPaintings[0].CanvasOriginalId.Should().Be( $"https://base/0/canvases/{canvasId}_1");
        dbManifest.CanvasPaintings[1].Id.Should().Be( $"{canvasId}_2");
        dbManifest.CanvasPaintings[2].CanvasOriginalId.Should().Be( $"https://base/0/canvases/{canvasId}_3");
        dbManifest.CanvasPaintings[3].Id.Should().Be( $"{canvasId}_4");
    }

    [Fact]
    public async Task Create_ReturnsAccepted_WhenManifestHasPipeline()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Pipeline = [new PipelineItem { Name = "text", Config = new PipelineConfig { Action = "Index" } }]
        };
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);

        // Act
        var result = await sut.Create(request, CancellationToken.None);

        // Assert
        result.WriteResult.Should().Be(WriteResult.Accepted);
    }

    [Fact]
    public async Task Create_CallsTextServicesAndCreatesPipelineJob_WhenManifestHasPipeline()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Pipeline = [new PipelineItem { Name = "text", Config = new PipelineConfig { Action = "Index" } }]
        };
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);

        // Act
        var result = await sut.Create(request, CancellationToken.None);

        // Assert
        A.CallTo(() => textServicesClient.CreateOrUpdateJob(A<PipelineJob>._, A<string>._, A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        var flatId = result.Entity.FlatId;
        var pipelineJob = presentationContext.PipelineJobs.FirstOrDefault(p => p.ResourceId == flatId);
        pipelineJob.Should().NotBeNull();
        pipelineJob!.Status.Should().Be(PipelineJobStatus.Waiting);
        pipelineJob.Config!.Action.Should().Be("Index");
        pipelineJob.GetJobId().Should().Be($"{Customer}/iiif/{flatId}");
        result.Entity.Pipeline.Should().ContainSingle(p => p.Name == PipelineHelper.TextPipelineName && p.Status == "Waiting");
    }

    [Fact]
    public async Task Create_ReturnsError_AndDoesNotPersistManifest_WhenTextServiceSubmissionFails()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Pipeline = [new PipelineItem { Name = "text", Config = new PipelineConfig { Action = "Index" } }]
        };
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);
        A.CallTo(() => textServicesClient.CreateOrUpdateJob(A<PipelineJob>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(false);

        // Act
        var result = await sut.Create(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.WriteResult.Should().Be(WriteResult.Error);
        result.Error.Should().Contain("pipeline job");

        // Manifest and pipeline job should be rolled back — resubmitting the same slug must not conflict
        presentationContext.Hierarchy.Any(h => h.Slug == slug).Should().BeFalse();
        presentationContext.PipelineJobs.Any(p => p.ResourceId == resourceId).Should().BeFalse();

        // Staged S3 objects must be cleaned up
        A.CallTo(() => manifestStorageManager.DeleteStagedManifest(A<Models.Database.Collections.Manifest>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_DoesNotCallTextServices_WhenManifestHasNoPipeline()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items = [new Canvas { Id = "https://base/0/canvases/canvas-1" }]
        };
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);

        // Act
        await sut.Create(request, CancellationToken.None);

        // Assert
        A.CallTo(() => textServicesClient.CreateOrUpdateJob(A<PipelineJob>._, A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Create_SavesManifestToStaging_WhenPipelineIsSet()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Pipeline = [new PipelineItem { Name = "text", Config = new PipelineConfig { Action = "Index" } }]
        };
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);

        // Act
        await sut.Create(request, CancellationToken.None);

        // Assert
        A.CallTo(() => manifestStorageManager.SaveManifestInStorage(
                A<IIIFManifest>._, A<DbManifest>._, null, true, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_AddsNewPipelineJob_WhenJobAlreadyExistsForManifest()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();

        // First create
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Pipeline = [new PipelineItem { Name = "text", Config = new PipelineConfig { Action = "Index" } }]
        };
        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);
        var firstResult = await sut.Create(request, CancellationToken.None);
        var flatId = firstResult.Entity.FlatId;

        // Second create (update path) — resubmit the same manifest with pipeline
        var updateManifest = new PresentationManifest
        {
            Slug = slug,
            Pipeline = [new PipelineItem { Name = "text", Config = new PipelineConfig { Action = "Index" } }]
        };
        var etag = presentationContext.Manifests.First(m => m.Id == flatId).Etag.ToString();
        var updateRequest = new UpsertManifestRequest(flatId, etag, Customer, updateManifest, updateManifest.AsJson(), false);

        // Act
        var result = await sut.Upsert(updateRequest, CancellationToken.None);

        // Assert
        result.WriteResult.Should().Be(WriteResult.Accepted);
        A.CallTo(() => textServicesClient.CreateOrUpdateJob(A<PipelineJob>._, A<string>._, A<string>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();

        var jobs = presentationContext.PipelineJobs.Where(p => p.ResourceId == flatId).ToList();
        jobs.Should().HaveCount(2, "each resubmission creates a new job record for history");
        jobs.Should().AllSatisfy(j => j.Status.Should().Be(PipelineJobStatus.Waiting));
    }
}
