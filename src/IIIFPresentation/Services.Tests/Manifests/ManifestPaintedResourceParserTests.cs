using Core.Exceptions;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Logging.Abstractions;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Services.Manifests;
using CanvasPainting = Models.Database.CanvasPainting;
using PresCanvasPainting = Models.API.Manifest.CanvasPainting;

namespace Services.Tests.Manifests;

public class ManifestPaintedResourceParserTests
{
    private readonly ManifestPaintedResourceParser sut = new(new NullLogger<ManifestItemsParser>());
    private const int CustomerId = 1234;
    private const int DefaultSpace = 10;
    private readonly string[] assetIds = ["frodo", "merry", "pippin", "sam", "gandalf", "balrog"];

    [Fact]
    public void Parse_ReturnsEmptyEnumerable_IfItemsNull()
        => sut.ParseToCanvasPainting(new PresentationManifest(), CustomerId).Should().BeEmpty();

    [Fact]
    public void Parse_ReturnsEmptyEnumerable_IfItemsEmpty()
        => sut.ParseToCanvasPainting(new PresentationManifest(), CustomerId).Should().BeEmpty();

    [Fact]
    public void Parse_Throws_InvalidCanvasId()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = GetAsset(),
                    CanvasPainting = new PresCanvasPainting { CanvasId = "https://invalid/format" }
                }
            ]
        };

        Action action = () => sut.ParseToCanvasPainting(manifest, CustomerId);
        action.Should().Throw<InvalidCanvasIdException>();
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

        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = null,
                AssetId = new AssetId(CustomerId, DefaultSpace, assetIds[0]),
                CanvasOrder = 0,
                ChoiceOrder = null,
                Target = null,
                Ingesting = true,
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId);

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

        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = null,
                AssetId = new AssetId(CustomerId, DefaultSpace, assetIds[0]),
                CanvasOrder = 0,
                ChoiceOrder = null,
                Target = null,
                Ingesting = true,
            },
            new()
            {
                CanvasOriginalId = null,
                AssetId = new AssetId(CustomerId, DefaultSpace + 1, assetIds[1]),
                CanvasOrder = 1,
                ChoiceOrder = null,
                Target = null,
                Ingesting = true,
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId);

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
                    }
                }
            ]
        };

        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = null,
                Id = "one",
                AssetId = new AssetId(CustomerId, DefaultSpace, assetIds[0]),
                CanvasOrder = 0,
                StaticHeight = 1023,
                StaticWidth = 513,
                Label = new LanguageMap("en", "label"),
                Thumbnail = new Uri("https://localhost/thumbnail"),
                ChoiceOrder = null,
                Target = null,
                Ingesting = true,
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId);

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
                    }
                }
            ]
        };

        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = null,
                Id = "one",
                AssetId = new AssetId(CustomerId, DefaultSpace, assetIds[0]),
                CanvasOrder = 0,
                StaticHeight = 1023,
                StaticWidth = 513,
                Label = new LanguageMap("en", "label"),
                Thumbnail = new Uri("https://localhost/thumbnail"),
                ChoiceOrder = null,
                Target = null,
                Ingesting = true,
            },
            new()
            {
                CanvasOriginalId = null,
                Id = "two",
                AssetId = new AssetId(CustomerId, DefaultSpace + 1, assetIds[1]),
                CanvasOrder = 0,
                StaticHeight = 10,
                StaticWidth = 12,
                Label = new LanguageMap("en", "label2"),
                Thumbnail = new Uri("https://localhost/thumbnail2"),
                ChoiceOrder = null,
                Target = null,
                Ingesting = true,
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId);

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
                    }
                }
            ]
        };

        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = null,
                Id = "one",
                AssetId = new AssetId(CustomerId, DefaultSpace, assetIds[0]),
                CanvasOrder = 0,
                StaticHeight = 1023,
                StaticWidth = 513,
                Label = new LanguageMap("en", "label"),
                Thumbnail = new Uri("https://localhost/thumbnail"),
                ChoiceOrder = 1,
                Target = null,
                Ingesting = true,
            },
            new()
            {
                CanvasOriginalId = null,
                Id = "one",
                AssetId = new AssetId(CustomerId, DefaultSpace + 1, assetIds[1]),
                CanvasOrder = 0,
                StaticHeight = 10,
                StaticWidth = 12,
                Label = new LanguageMap("en", "label2"),
                Thumbnail = new Uri("https://localhost/thumbnail2"),
                ChoiceOrder = 2,
                Target = null,
                Ingesting = true,
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId);
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

        var expected = new List<CanvasPainting>
        {
            new()
            {
                Id = "one",
                CanvasOriginalId = null,
                AssetId = new AssetId(CustomerId, DefaultSpace, assetIds[0]),
                CanvasOrder = 1,
                ChoiceOrder = null,
                Target = "#xywh=200,2000,200,200",
                Ingesting = true,
            },
            new()
            {
                Id = "one",
                CanvasOriginalId = null,
                AssetId = new AssetId(CustomerId, DefaultSpace + 1, assetIds[1]),
                CanvasOrder = 2,
                ChoiceOrder = null,
                Target = "#xywh=0,0,200,200",
                Ingesting = true,
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId);
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
                    }
                }
            ]
        };

        var expected = new List<CanvasPainting>
        {
            new()
            {
                Id = parsedCanvasId,
                CanvasOriginalId = null,
                AssetId = new AssetId(CustomerId, DefaultSpace, assetIds[0]),
                CanvasOrder = 18,
                ChoiceOrder = null,
                Ingesting = true,
                CanvasLabel = new LanguageMap("en", "ms125 9r fragments and multi-spectral"),
                Label = new LanguageMap("en", "ms125 9r background"),
            },
            new()
            {
                Id = parsedCanvasId,
                CanvasOriginalId = null,
                AssetId = new AssetId(CustomerId, DefaultSpace, assetIds[1]),
                CanvasOrder = 19,
                ChoiceOrder = null,
                Label = new LanguageMap("en", "ms125 9r fragment 3"),
                Target = "xywh=800,1000,900,900",
                Ingesting = true,
            },
            new()
            {
                Id = parsedCanvasId,
                CanvasOriginalId = null,
                AssetId = new AssetId(CustomerId, DefaultSpace, assetIds[2]),
                CanvasOrder = 20,
                ChoiceOrder = 1,
                Label = new LanguageMap("en", "ms125 9r fragment 2 natural"),
                Target = "xywh=300,500,650,900",
                Ingesting = true,
            },
            new()
            {
                Id = parsedCanvasId,
                CanvasOriginalId = null,
                AssetId = new AssetId(CustomerId, DefaultSpace, assetIds[3]),
                CanvasOrder = 20,
                ChoiceOrder = 2,
                Label = new LanguageMap("en", "ms125 9r fragment 2 IR"),
                Target = "xywh=300,500,650,900",
                Ingesting = true,
            },
            new()
            {
                Id = parsedCanvasId,
                CanvasOriginalId = null,
                AssetId = new AssetId(CustomerId, DefaultSpace, assetIds[4]),
                CanvasOrder = 21,
                ChoiceOrder = null,
                Label = new LanguageMap("en", "ms125 9r fragment 1"),
                Target = "xywh=100,100,1000,500",
                Ingesting = true,
            },
        };
        
        var canvasPaintings = sut.ParseToCanvasPainting(manifest, CustomerId);
        canvasPaintings.Should().BeEquivalentTo(expected);
    }

    private JObject GetAsset(int? space = null, string? id = null)
        => new() { ["id"] = id ?? assetIds[0], ["space"] = space ?? DefaultSpace };
}
