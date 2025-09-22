using Core.Exceptions;
using FakeItEasy;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository.Paths;
using Services.Manifests;
using Services.Manifests.Model;
using Test.Helpers.Helpers;
using CanvasPainting = Models.Database.CanvasPainting;
using PresCanvasPainting = Models.API.Manifest.CanvasPainting;

namespace Services.Tests.Manifests;

public class ManifestPaintedResourceParserTests
{
    private readonly ManifestPaintedResourceParser sut;
    private const int CustomerId = 1234;
    private const int DefaultSpace = 10;
    private readonly string[] assetIds = ["frodo", "merry", "pippin", "sam", "gandalf", "balrog"];
    
    public ManifestPaintedResourceParserTests()
    {
        var pathRewriteParser = new PathRewriteParser(Options.Create(PathRewriteOptions.Default),
            new NullLogger<PathRewriteParser>());

        sut = new ManifestPaintedResourceParser(pathRewriteParser, A.Fake<IPresentationPathGenerator>(),
            new NullLogger<ManifestPaintedResourceParser>());
    }

    [Fact]
    public void Parse_ReturnsEmptyEnumerable_IfItemsNull()
        => sut.ParseToCanvasPainting(new PresentationManifest(), CustomerId, null!).Should().BeEmpty();

    [Fact]
    public void Parse_ReturnsEmptyEnumerable_IfItemsEmpty()
        => sut.ParseToCanvasPainting(new PresentationManifest(), CustomerId, null!).Should().BeEmpty();

    [Theory]
    [InlineData("https://foo.com/example/1/canvases/canvas")]
    [InlineData("https://default.com/additionalElement/1/canvases/canvas")]
    [InlineData("https://dlcs.example/1/random/foo/bar/baz")]
    [InlineData("https://invalid/format")]
    [InlineData("https://dlcs.example/1/canvases/")]
    [InlineData("https://dlcs.example/1/canvases")]
    [InlineData("https://dlcs.example/1/canvases/canvas/")]
    public void Parse_Throws_InvalidCanvasId(string canvasId)
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = GetAsset(),
                    CanvasPainting = new PresCanvasPainting { CanvasId = canvasId }
                }
            ]
        };

        Action action = () => sut.ParseToCanvasPainting(manifest, CustomerId, null!);
        action.Should().Throw<InvalidCanvasIdException>();
    }
    
    [Theory]
    [InlineData("https://default.com/1/canvases/canvas")]
    [InlineData("https://foo.com/foo/1/canvases/canvas")]
    [InlineData("https://no-customer.com/canvases/canvas")]
    [InlineData("https://additional-path-no-customer.com/foo/canvases/canvas")]
    [InlineData("https://dlcs.example/1/canvases/canvas?foo=bar")]
    [InlineData("canvas")]
    public void Parse_Parses_WhenRewrittenCanvasId(string canvasId)
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = GetAsset(),
                    CanvasPainting = new PresCanvasPainting { CanvasId = canvasId }
                }
            ]
        };

        var parsed = sut.ParseToCanvasPainting(manifest, CustomerId, null!);
        
        parsed.First().Id.Should().Be("canvas");
    }
    
    [Fact]
    public void Parse_SingleItem_AssetOnly()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource { Asset = GetAsset() }
            ]
        };

        var expected = new List<InterimCanvasPainting>
        {
            new()
            {
                CanvasOriginalId = null,
                AssetId = assetIds[0],
                Space = DefaultSpace,
                CustomerId = CustomerId,
                CanvasOrder = 0,
                ChoiceOrder = null,
                Target = null,
                Ingesting = false,
                ImplicitOrder = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId, null!);

        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Parse_MultiCanvas_AssetOnly()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource { Asset = GetAsset() },
                new PaintedResource { Asset = GetAsset(DefaultSpace + 1, assetIds[1]) }
            ]
        };

        var expected = new List<InterimCanvasPainting>
        {
            new()
            {
                CanvasOriginalId = null,
                AssetId = assetIds[0],
                CustomerId = CustomerId,
                Space = DefaultSpace,
                CanvasOrder = 0,
                ChoiceOrder = null,
                Target = null,
                Ingesting = false,
                ImplicitOrder = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
            new()
            {
                CanvasOriginalId = null,
                AssetId = assetIds[1],
                CustomerId = CustomerId,
                Space = DefaultSpace + 1,
                CanvasOrder = 1,
                ChoiceOrder = null,
                Target = null,
                Ingesting = false,
                ImplicitOrder = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId, null!);

        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Parse_SingleItem()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = GetAsset(),
                    CanvasPainting = new PresCanvasPainting
                    {
                        CanvasId = $"http://localhost/{CustomerId}/canvases/one",
                        CanvasOrder = 0,
                        StaticHeight = 1023,
                        StaticWidth = 513,
                        Label = new LanguageMap("en", "label"),
                        Thumbnail = "https://localhost/thumbnail",
                        Ingesting = true
                    }
                }
            ]
        };

        var expected = new List<InterimCanvasPainting>
        {
            new()
            {
                CanvasOriginalId = null,
                Id = "one",
                AssetId = assetIds[0],
                Space = DefaultSpace,
                CustomerId = CustomerId,
                CanvasOrder = 0,
                StaticHeight = 1023,
                StaticWidth = 513,
                Label = new LanguageMap("en", "label"),
                Thumbnail = new Uri("https://localhost/thumbnail"),
                ChoiceOrder = null,
                Target = null,
                Ingesting = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId, null!);

        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Parse_MultiItem_CanvasId()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = GetAsset(),
                    CanvasPainting = new PresCanvasPainting
                    {
                        CanvasId = $"http://localhost/{CustomerId}/canvases/one",
                        CanvasOrder = 0,
                        StaticHeight = 1023,
                        StaticWidth = 513,
                        Label = new LanguageMap("en", "label"),
                        Thumbnail = "https://localhost/thumbnail",
                        Ingesting = true
                    }
                },
                new PaintedResource
                {
                    Asset = GetAsset(DefaultSpace + 1, assetIds[1]),
                    CanvasPainting = new PresCanvasPainting
                    {
                        CanvasId = $"http://localhost/{CustomerId}/canvases/two",
                        CanvasOrder = 0,
                        StaticHeight = 10,
                        StaticWidth = 12,
                        Label = new LanguageMap("en", "label2"),
                        Thumbnail = "https://localhost/thumbnail2",
                        Ingesting = true
                    }
                }
            ]
        };

        var expected = new List<InterimCanvasPainting>
        {
            new()
            {
                CanvasOriginalId = null,
                Id = "one",
                AssetId = assetIds[0],
                Space = DefaultSpace,
                CustomerId = CustomerId,
                CanvasOrder = 0,
                StaticHeight = 1023,
                StaticWidth = 513,
                Label = new LanguageMap("en", "label"),
                Thumbnail = new Uri("https://localhost/thumbnail"),
                ChoiceOrder = null,
                Target = null,
                Ingesting = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
            new()
            {
                CanvasOriginalId = null,
                Id = "two",
                AssetId = assetIds[1],
                Space = DefaultSpace + 1,
                CustomerId = CustomerId,
                CanvasOrder = 0,
                StaticHeight = 10,
                StaticWidth = 12,
                Label = new LanguageMap("en", "label2"),
                Thumbnail = new Uri("https://localhost/thumbnail2"),
                ChoiceOrder = null,
                Target = null,
                Ingesting = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId, null!);

        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Parse_SingleCanvasWithChoices()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = GetAsset(),
                    CanvasPainting = new PresCanvasPainting
                    {
                        CanvasId = $"http://localhost/{CustomerId}/canvases/one",
                        CanvasOrder = 0,
                        ChoiceOrder = 1,
                        StaticHeight = 1023,
                        StaticWidth = 513,
                        Label = new LanguageMap("en", "label"),
                        Thumbnail = "https://localhost/thumbnail",
                        Ingesting = true
                    }
                },
                new PaintedResource
                {
                    Asset = GetAsset(DefaultSpace + 1, assetIds[1]),
                    CanvasPainting = new PresCanvasPainting
                    {
                        CanvasId = $"http://localhost/{CustomerId}/canvases/one",
                        CanvasOrder = 0,
                        ChoiceOrder = 2,
                        StaticHeight = 10,
                        StaticWidth = 12,
                        Label = new LanguageMap("en", "label2"),
                        Thumbnail = "https://localhost/thumbnail2",
                        Ingesting = true
                    }
                }
            ]
        };

        var expected = new List<InterimCanvasPainting>
        {
            new()
            {
                CanvasOriginalId = null,
                Id = "one",
                AssetId = assetIds[0],
                Space = DefaultSpace,
                CustomerId = CustomerId,
                CanvasOrder = 0,
                StaticHeight = 1023,
                StaticWidth = 513,
                Label = new LanguageMap("en", "label"),
                Thumbnail = new Uri("https://localhost/thumbnail"),
                ChoiceOrder = 1,
                Target = null,
                Ingesting = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
            new()
            {
                CanvasOriginalId = null,
                Id = "one",
                AssetId = assetIds[1],
                Space = DefaultSpace + 1,
                CustomerId = CustomerId,
                CanvasOrder = 0,
                StaticHeight = 10,
                StaticWidth = 12,
                Label = new LanguageMap("en", "label2"),
                Thumbnail = new Uri("https://localhost/thumbnail2"),
                ChoiceOrder = 2,
                Target = null,
                Ingesting = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId, null!);
        canvasPaintings.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Parse_MultiImageComposition()
    {
        // Composite 
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = GetAsset(),
                    CanvasPainting = new PresCanvasPainting
                    {
                        CanvasId = $"http://localhost/{CustomerId}/canvases/one",
                        CanvasOrder = 1,
                        Target = "#xywh=200,2000,200,200",
                    },
                },
                new PaintedResource
                {
                    Asset = GetAsset(DefaultSpace + 1, assetIds[1]),
                    CanvasPainting = new PresCanvasPainting
                    {
                        CanvasId = $"http://localhost/{CustomerId}/canvases/one",
                        CanvasOrder = 2,
                        Target = "#xywh=0,0,200,200",
                    }
                }
            ]
        };

        var expected = new List<InterimCanvasPainting>
        {
            new()
            {
                Id = "one",
                CanvasOriginalId = null,
                AssetId = assetIds[0],
                Space = DefaultSpace,
                CustomerId = CustomerId,
                CanvasOrder = 1,
                ChoiceOrder = null,
                Target = "#xywh=200,2000,200,200",
                Ingesting = false,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
            new()
            {
                Id = "one",
                CanvasOriginalId = null,
                AssetId = assetIds[1],
                Space = DefaultSpace + 1,
                CustomerId = CustomerId,
                CanvasOrder = 2,
                ChoiceOrder = null,
                Target = "#xywh=0,0,200,200",
                Ingesting = false,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId, null!);
        canvasPaintings.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Parse_MultiImageCompositionAndChoice_SameCanvas()
    {
        // Based on https://github.com/dlcs/docs/blob/wip-skeleton/public/manifest-builder/database.py#L90-L99
        var fullCanvasId = $"http://localhost/{CustomerId}/canvases/one";
        const string parsedCanvasId = "one";
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = GetAsset(),
                    CanvasPainting = new PresCanvasPainting
                    {
                        CanvasId = fullCanvasId,
                        CanvasOrder = 18,
                        CanvasLabel = new LanguageMap("en", "ms125 9r fragments and multi-spectral"),
                        Label = new LanguageMap("en", "ms125 9r background"),
                        Ingesting = true
                    },
                },
                new PaintedResource
                {
                    Asset = GetAsset(id: assetIds[1]),
                    CanvasPainting = new PresCanvasPainting
                    {
                        CanvasId = fullCanvasId,
                        CanvasOrder = 19,
                        Label = new LanguageMap("en", "ms125 9r fragment 3"),
                        Target = "xywh=800,1000,900,900",
                        Ingesting = true
                    }
                },
                new PaintedResource
                {
                    Asset = GetAsset(id: assetIds[2]),
                    CanvasPainting = new PresCanvasPainting
                    {
                        CanvasId = fullCanvasId,
                        CanvasOrder = 20,
                        ChoiceOrder = 1,
                        Label = new LanguageMap("en", "ms125 9r fragment 2 natural"),
                        Target = "xywh=300,500,650,900",
                        Ingesting = true
                    }
                },
                new PaintedResource
                {
                    Asset = GetAsset(id: assetIds[3]),
                    CanvasPainting = new PresCanvasPainting
                    {
                        CanvasId = fullCanvasId,
                        CanvasOrder = 20,
                        ChoiceOrder = 2,
                        Label = new LanguageMap("en", "ms125 9r fragment 2 IR"),
                        Target = "xywh=300,500,650,900",
                        Ingesting = true
                    }
                },
                new PaintedResource
                {
                    Asset = GetAsset(id: assetIds[4]),
                    CanvasPainting = new PresCanvasPainting
                    {
                        CanvasId = fullCanvasId,
                        CanvasOrder = 21,
                        Label = new LanguageMap("en", "ms125 9r fragment 1"),
                        Target = "xywh=100,100,1000,500",
                        Ingesting = true
                    }
                }
            ]
        };

        var expected = new List<InterimCanvasPainting>
        {
            new()
            {
                Id = parsedCanvasId,
                CanvasOriginalId = null,
                AssetId = assetIds[0],
                Space = DefaultSpace,
                CustomerId = CustomerId,
                CanvasOrder = 18,
                ChoiceOrder = null,
                Ingesting = true,
                CanvasLabel = new LanguageMap("en", "ms125 9r fragments and multi-spectral"),
                Label = new LanguageMap("en", "ms125 9r background"),
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
            new()
            {
                Id = parsedCanvasId,
                CanvasOriginalId = null,
                AssetId = assetIds[1],
                Space = DefaultSpace,
                CustomerId = CustomerId,
                CanvasOrder = 19,
                ChoiceOrder = null,
                Label = new LanguageMap("en", "ms125 9r fragment 3"),
                Target = "xywh=800,1000,900,900",
                Ingesting = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
            new()
            {
                Id = parsedCanvasId,
                CanvasOriginalId = null,
                AssetId = assetIds[2],
                Space = DefaultSpace,
                CustomerId = CustomerId,
                CanvasOrder = 20,
                ChoiceOrder = 1,
                Label = new LanguageMap("en", "ms125 9r fragment 2 natural"),
                Target = "xywh=300,500,650,900",
                Ingesting = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
            new()
            {
                Id = parsedCanvasId,
                CanvasOriginalId = null,
                AssetId = assetIds[3],
                Space = DefaultSpace,
                CustomerId = CustomerId,
                CanvasOrder = 20,
                ChoiceOrder = 2,
                Label = new LanguageMap("en", "ms125 9r fragment 2 IR"),
                Target = "xywh=300,500,650,900",
                Ingesting = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
            new()
            {
                Id = parsedCanvasId,
                CanvasOriginalId = null,
                AssetId = assetIds[4],
                Space = DefaultSpace,
                CustomerId = CustomerId,
                CanvasOrder = 21,
                ChoiceOrder = null,
                Label = new LanguageMap("en", "ms125 9r fragment 1"),
                Target = "xywh=100,100,1000,500",
                Ingesting = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId, null!);
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Parse_MultiCanvas_WithMixOfImplicitOrdering()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource { Asset = GetAsset(), CanvasPainting = new PresCanvasPainting {CanvasOrder = 1}},
                new PaintedResource { Asset = GetAsset(DefaultSpace + 1, assetIds[1]) }
            ]
        };

        var expected = new List<InterimCanvasPainting>
        {
            new()
            {
                CanvasOriginalId = null,
                AssetId = assetIds[0],
                CustomerId = CustomerId,
                Space = DefaultSpace,
                CanvasOrder = 1,
                ChoiceOrder = null,
                Target = null,
                Ingesting = false,
                ImplicitOrder = false,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
            new()
            {
                CanvasOriginalId = null,
                AssetId = assetIds[1],
                CustomerId = CustomerId,
                Space = DefaultSpace + 1,
                CanvasOrder = 1,
                ChoiceOrder = null,
                Target = null,
                Ingesting = false,
                ImplicitOrder = true,
                CanvasPaintingType = CanvasPaintingType.PaintedResource
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId, null!);

        canvasPaintings.Should().BeEquivalentTo(expected);
    }

    private JObject GetAsset(int? space = null, string? id = null)
        => new() { ["id"] = id ?? assetIds[0], ["space"] = space ?? DefaultSpace };
}
