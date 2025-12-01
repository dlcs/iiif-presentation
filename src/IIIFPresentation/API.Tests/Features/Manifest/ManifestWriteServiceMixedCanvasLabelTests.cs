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
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
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
using Test.Helpers.Settings;

namespace API.Tests.Features.Manifest;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ManifestWriteServiceMixedCanvasLabelTests
{
    private readonly ManifestWriteService sut;
    private readonly PresentationContext presentationContext;
    private const int Customer = 1;
    private const int NewlyCreatedSpace = 500;
    private readonly DlcsSettings dlcsSettings;
    private readonly IDlcsApiClient dlcsClient;

    public ManifestWriteServiceMixedCanvasLabelTests(PresentationContextFixture dbFixture)
    {
        presentationContext = dbFixture.DbContext;
        dlcsSettings = DefaultSettings.DlcsSettings();

        var typedPathTemplateOptions = Options.Create(PathRewriteOptions.Default);
        
        var sqidsEncoder = new SqidsEncoder<long>();
        var idGenerator = new SqidsGenerator(sqidsEncoder, new NullLogger<SqidsGenerator>());
        
        var identityManager = new IdentityManager(idGenerator, presentationContext, new NullLogger<IdentityManager>());
        var iiifS3Service = A.Fake<IIIIFS3Service>();
        
        var presentationGenerator =
            new TestPresentationConfigGenerator("https://localhost:5000", PathRewriteOptions.Default);
        
        var pathRewriteParser = new PathRewriteParser(typedPathTemplateOptions, new NullLogger<PathRewriteParser>());

        var manifestItemsParser = new ManifestItemsParser(pathRewriteParser, presentationGenerator,
            new PaintableAssetIdentifier(OptionsHelpers.GetOptionsMonitor(dlcsSettings),
                new NullLogger<PaintableAssetIdentifier>()),
            Options.Create(new PathSettings { PresentationApiUrl = new Uri("https://base") }),
            new NullLogger<ManifestItemsParser>());
        
        var manifestPaintedResourceParser = new ManifestPaintedResourceParser(pathRewriteParser, presentationGenerator,
            new NullLogger<ManifestPaintedResourceParser>());

        var canvasPaintingMerger = new CanvasPaintingMerger(pathRewriteParser);

        var canvasPaintingResolver = new CanvasPaintingResolver(identityManager, manifestItemsParser,
            manifestPaintedResourceParser, canvasPaintingMerger, new NullLogger<CanvasPaintingResolver>());
        
        dlcsClient = A.Fake<IDlcsApiClient>();
        
            
        var managedResultFinder = new ManagedAssetResultFinder(dlcsClient, presentationContext,
            new NullLogger<ManagedAssetResultFinder>());
        var dlcsManifestCoordinator = new DlcsManifestCoordinator(dlcsClient, presentationContext, managedResultFinder,
            new NullLogger<DlcsManifestCoordinator>());

        var parentSlugParser = A.Fake<IParentSlugParser>();

        var manifestStorageManager = A.Fake<IManifestStorageManager>();
        var settingsBasedPathGenerator = new SettingsBasedPathGenerator(Options.Create(dlcsSettings),
            new SettingsDrivenPresentationConfigGenerator(Options.Create(new PathSettings()
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
    public async Task Create_SuccessfullyCreatesSingleItemManifest_WhenMatchingCanvasLabel()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "toMatch",
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasLabel = label
                    },
                    Asset = JObject.Parse(@"{""id"": ""first""}")
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
        ingestedManifest.Entity.Items.First().Id.Should().Be("https://presentation.api/1/canvases/toMatch");
        var paintedResource = ingestedManifest.Entity.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
        paintedResource.CanvasPainting.Label.Should().BeNull();
    }
    
    [Fact]
    public async Task Create_SuccessfullyCreatesSingleItemManifest_WhenMatchingLabel()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "toMatch",
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        Label = label
                    },
                    Asset = JObject.Parse(@"{""id"": ""first""}")
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
        ingestedManifest.Entity.Items.First().Id.Should().Be("https://presentation.api/1/canvases/toMatch");
        var paintedResource = ingestedManifest.Entity.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be(label.First().Key);
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
    }
    
    [Fact]
    public async Task Create_SuccessfullyCreatesSingleItemManifest_WhenNonMatchingLabel()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "toMatch",
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        Label = new LanguageMap("anotherLabel", "label to not match"),
                    },
                    Asset = JObject.Parse(@"{""id"": ""first""}")
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
        ingestedManifest.Entity.Items.First().Id.Should().Be("https://presentation.api/1/canvases/toMatch");
        var paintedResource = ingestedManifest.Entity.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
    }
    
    [Fact]
    public async Task Create_SuccessfullyCreatesSingleItemManifest_WhenMatchingCanvasLabelAndNonMatchingLabel()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "toMatch",
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        Label = new LanguageMap("anotherLabel", "label to not match"),
                        CanvasLabel = label
                    },
                    Asset = JObject.Parse(@"{""id"": ""first""}")
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
        ingestedManifest.Entity.Items.First().Id.Should().Be("https://presentation.api/1/canvases/toMatch");
        var paintedResource = ingestedManifest.Entity.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
    }
    
    [Fact]
    public async Task Create_SuccessfullyCreatesSingleItemManifest_WhenMatchingCanvasLabelAndLabel()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "toMatch",
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        Label = label,
                        CanvasLabel = label
                    },
                    Asset = JObject.Parse(@"{""id"": ""first""}")
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
        ingestedManifest.Entity.Items.First().Id.Should().Be("https://presentation.api/1/canvases/toMatch");
        var paintedResource = ingestedManifest.Entity.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be(label.First().Key);
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
    }
    
    [Fact]
    public async Task Create_ThrowsError_WhenNonMatchingCanvasLabelAndMatchingLabel()
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "toMatch",
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        Label = label,
                        CanvasLabel = new LanguageMap("anotherLabel", "label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""first""}")
                }
            ]
        };

        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);

        // Act
        var ingestedManifest = await sut.Create(request, CancellationToken.None);

        // Assert
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should().Be("Canvas painting with id toMatch does not have a matching canvas label");
    }
    
    [Theory]
    [InlineData(0, 0, 0, 1, "_1")]
    [InlineData(0, 0, 1, 0, "_2")]
    [InlineData(0, null, 1, null, "_3")]
    public async Task Create_SuccessfullyCreatesMultiItemManifest_WhenMatchingLabel(int firstCanvasOrder, 
        int? firstChoiceOrder, int secondCanvasOrder, int? secondChoiceOrder, string slugAppend)
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        slug += slugAppend;
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "toMatch",
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasOrder = firstCanvasOrder,
                        ChoiceOrder = firstChoiceOrder,
                        Label = new LanguageMap("anotherLabel", "label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""first""}")
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasOrder = secondCanvasOrder,
                        ChoiceOrder = secondChoiceOrder,
                        Label = new LanguageMap("anotherLabel2", "second label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""second""}")
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
        ingestedManifest.Entity.Items.First().Id.Should().Be("https://presentation.api/1/canvases/toMatch");
        var paintedResource = ingestedManifest.Entity.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
        var secondPaintedResource = ingestedManifest.Entity.PaintedResources.Last();
        secondPaintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        secondPaintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel2");
    }
    
    [Theory]
    [InlineData(0, 0, 0, 1, "_1")]
    [InlineData(0, 0, 1, 0, "_2")]
    [InlineData(0, null, 1, null, "_3")]
    public async Task Create_SuccessfullyCreatesMultiItemManifest_WhenMatchingCanvasLabel(int firstCanvasOrder, 
        int? firstChoiceOrder, int secondCanvasOrder, int? secondChoiceOrder, string slugAppend)
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        slug += slugAppend;
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "toMatch",
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasOrder = firstCanvasOrder,
                        ChoiceOrder = firstChoiceOrder,
                        Label = new LanguageMap("anotherLabel", "label to not match"),
                        CanvasLabel = label
                    },
                    Asset = JObject.Parse(@"{""id"": ""first""}")
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasOrder = secondCanvasOrder,
                        ChoiceOrder = secondChoiceOrder,
                        Label = new LanguageMap("anotherLabel2", "second label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""second""}")
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
        ingestedManifest.Entity.Items.First().Id.Should().Be("https://presentation.api/1/canvases/toMatch");
        var paintedResource = ingestedManifest.Entity.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
        var secondPaintedResource = ingestedManifest.Entity.PaintedResources.Last();
        secondPaintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        secondPaintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel2");
    }
    
    [Theory]
    [InlineData(0, 0, 0, 1, "_1")]
    [InlineData(0, 0, 1, 0, "_2")]
    [InlineData(0, null, 1, null, "_3")]
    public async Task Create_SuccessfullyCreatesMultiItemManifest_WhenMatchingCanvasLabelFromNotTheFirstCanvas(int firstCanvasOrder, 
        int? firstChoiceOrder, int secondCanvasOrder, int? secondChoiceOrder, string slugAppend)
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        slug += slugAppend;
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "toMatch",
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasOrder = firstCanvasOrder,
                        ChoiceOrder = firstChoiceOrder,
                        Label = new LanguageMap("anotherLabel", "label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""first""}")
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasOrder = secondCanvasOrder,
                        ChoiceOrder = secondChoiceOrder,
                        Label = new LanguageMap("anotherLabel2", "second label to not match"),
                        CanvasLabel = label
                    },
                    Asset = JObject.Parse(@"{""id"": ""second""}")
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
        ingestedManifest.Entity.Items.First().Id.Should().Be("https://presentation.api/1/canvases/toMatch");
        var paintedResource = ingestedManifest.Entity.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
        paintedResource.CanvasPainting.CanvasLabel.Should().BeNull();
        var secondPaintedResource = ingestedManifest.Entity.PaintedResources.Last();
        secondPaintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        secondPaintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel2");
        secondPaintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
    }
    
    [Theory]
    [InlineData(0, 1, 0, 0, "_1")]
    [InlineData(1, 0, 0, 0, "_2")]
    [InlineData(1, null, 0, null, "_3")]
    public async Task Create_SuccessfullyCreatesMultiItemManifest_WhenMatchingCanvasLabelOrderedBadly(int firstCanvasOrder, 
        int? firstChoiceOrder, int secondCanvasOrder, int? secondChoiceOrder, string slugAppend)
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        slug += slugAppend;
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "toMatch",
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasOrder = firstCanvasOrder,
                        ChoiceOrder = firstChoiceOrder,
                        Label = new LanguageMap("anotherLabel", "label to not match"),
                        CanvasLabel = new LanguageMap("mismatch", "canvas label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""first""}")
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasOrder = secondCanvasOrder,
                        ChoiceOrder = secondChoiceOrder,
                        Label = new LanguageMap("anotherLabel2", "second label to not match"),
                        CanvasLabel = label
                    },
                    Asset = JObject.Parse(@"{""id"": ""second""}")
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
        ingestedManifest.Entity.Items.First().Id.Should().Be("https://presentation.api/1/canvases/toMatch");
        var paintedResource = ingestedManifest.Entity.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel2");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
        var secondPaintedResource = ingestedManifest.Entity.PaintedResources.Last();
        secondPaintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        secondPaintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
    }
    
    [Theory]
    [InlineData(0, 0, 0, 1, "_1")]
    [InlineData(0, 0, 1, 0, "_2")]
    [InlineData(0, null, 1, null, "_3")]
    public async Task Create_FailsToCreateMultiItemManifest_WhenNonMatchingCanvasLabelFromNotTheFirstCanvas(int firstCanvasOrder, 
        int? firstChoiceOrder, int secondCanvasOrder, int? secondChoiceOrder, string slugAppend)
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        slug += slugAppend;
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "toMatch",
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasOrder = firstCanvasOrder,
                        ChoiceOrder = firstChoiceOrder,
                        Label = new LanguageMap("anotherLabel", "label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""first""}")
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasOrder = secondCanvasOrder,
                        ChoiceOrder = secondChoiceOrder,
                        Label = new LanguageMap("anotherLabel2", "second label to not match"),
                        CanvasLabel = new LanguageMap("none matching canvas", "label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""second""}")
                }
            ]
        };

        var request = new UpsertManifestRequest(resourceId, null, Customer, manifest, manifest.AsJson(), true);

        // Act
        var ingestedManifest = await sut.Create(request, CancellationToken.None);

        // Assert
        ingestedManifest.Should().NotBeNull();
        ingestedManifest.Error.Should().Be("Canvas painting with id toMatch does not have a matching canvas label");
    }
    
    [Theory]
    [InlineData(0, 0, 0, 1, "_1")]
    [InlineData(0, 0, 1, 0, "_2")]
    [InlineData(0, null, 1, null, "_3")]
    public async Task Create_SuccessfullyCreatesMultiItemManifest_WhenNoCanvasLabelSet(int firstCanvasOrder, 
        int? firstChoiceOrder, int secondCanvasOrder, int? secondChoiceOrder, string slugAppend)
    {
        // Arrange
        var (slug, resourceId) = TestIdentifiers.SlugResource();
        slug += slugAppend;
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "toMatch",
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasOrder = firstCanvasOrder,
                        ChoiceOrder = firstChoiceOrder,
                        Label = new LanguageMap("anotherLabel", "label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""first""}")
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "toMatch",
                        CanvasOrder = secondCanvasOrder,
                        ChoiceOrder = secondChoiceOrder,
                        Label = new LanguageMap("anotherLabel2", "second label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""second""}")
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
        ingestedManifest.Entity.Items.First().Id.Should().Be("https://presentation.api/1/canvases/toMatch");
        var paintedResource = ingestedManifest.Entity.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
        var secondPaintedResource = ingestedManifest.Entity.PaintedResources.Last();
        secondPaintedResource.CanvasPainting.CanvasId.Should().Be($"https://localhost:5000/{Customer}/canvases/toMatch");
        secondPaintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel2");
        secondPaintedResource.CanvasPainting.CanvasLabel.Should().BeNull();
    }
}
