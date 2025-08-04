using Core.Exceptions;
using Core.IIIF;
using DLCS;
using FakeItEasy;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Repository.Paths;
using Models.DLCS;
using Services.Manifests;
using Services.Manifests.Helpers;
using Services.Manifests.Settings;
using Test.Helpers.Helpers;
using CanvasPainting = Models.Database.CanvasPainting;

namespace Services.Tests.Manifests;

public class ManifestItemsParserTests
{
    private readonly ManifestItemsParser sut;
    
    public ManifestItemsParserTests()
    {
        var pathRewriteParser =
            new PathRewriteParser(Options.Create(PathRewriteOptions.Default), new NullLogger<PathRewriteParser>());
        
        var pathSettings = new PathSettings
        {
            PresentationApiUrl = new Uri("https://localhost:7230"), CustomerPresentationApiUrl =
                new Dictionary<int, Uri>
                {
                    { 2, new Uri("https://foo.com") }
                }
        };

        sut = new ManifestItemsParser(pathRewriteParser,
            new TestPresentationConfigGenerator("http://base", PathRewriteOptions.Default),
            Options.Create(pathSettings), new NullLogger<ManifestItemsParser>());
    }

    private static readonly Dictionary<IPaintable, AssetId> EmptyRecognizedDictionary = new();
    [Fact]
    public void Parse_ReturnsEmptyEnumerable_IfItemsNull()
        => sut.ParseToCanvasPainting(new PresentationManifest(), [],123, EmptyRecognizedDictionary).Should().BeEmpty();

    [Fact]
    public void Parse_ReturnsEmptyEnumerable_IfItemsEmpty()
        => sut.ParseToCanvasPainting(new PresentationManifest { Items = [] }, [], 123, EmptyRecognizedDictionary).Should().BeEmpty();

    [Fact]
    public void Parse_ReturnsCanvasPainting_IfCanvasHasNoAnnotationPages()
        => sut.ParseToCanvasPainting(new PresentationManifest { Items = [new Canvas { Items = [] }] }, [], 123, EmptyRecognizedDictionary).Should().HaveCount(1);

    [Fact]
    public void Parse_ReturnsCanvasPainting_IfAnnotationPagesHaveNoAnnotations()
        => sut.ParseToCanvasPainting(new PresentationManifest { Items = [new Canvas { Items = [new AnnotationPage()] }] }, [], 123, EmptyRecognizedDictionary)
            .Should().HaveCount(1);

    [Fact]
    public void Parse_ReturnsCanvasPainting_IfAnnotationPagesHaveOnlyNonPaintingAnnotation()
        => sut.ParseToCanvasPainting(new PresentationManifest
            {
                Items = [new Canvas { Items = [new AnnotationPage { Items = [new TypeClassifyingAnnotation()] }] }]
            }, [], 123, EmptyRecognizedDictionary)
            .Should().HaveCount(1);

    [Theory]
    [InlineData("https://localhost:5000/123/canvases/foo", 123)]
    [InlineData("https://foo.com/2/canvases/foo", 2)]
    [InlineData("https://localhost:5000/2/canvases/foo", 2)]
    public void Parse_ReturnsCanvasPaintingWithId_IfCanvasIdRecognised(string host, int customerId)
        => sut.ParseToCanvasPainting(new PresentationManifest
        {
            Items = [new Canvas { Id = host }]
        }, [ new CanvasPainting { Id = "foo" }], customerId, EmptyRecognizedDictionary).Single().Id.Should().Be("foo");
    
    [Fact]
    public void Parse_ReturnsNullCanvasId_IfCanvasIdValidUriNotMatchedToPaintedResource()
        => sut.ParseToCanvasPainting(new PresentationManifest
        {
            Items = [new Canvas { Id = "https://localhost:5000/123/canvases/foo" }]
        }, [], 1, EmptyRecognizedDictionary).Single().Id.Should().BeNull();

    [Fact]
    public void Parse_ThrowsError_IfShortCanvasNotMatchedToPaintedResource()
    {
        // Act
        Action action = () => sut.ParseToCanvasPainting(new PresentationManifest
        {
            Items = [new Canvas { Id = "foo" }]
        }, [], 1, EmptyRecognizedDictionary);
        
        //Assert
        action.Should().ThrowExactly<InvalidCanvasIdException>().WithMessage("The canvas id is not a valid URI, and cannot be matched with a painted resource");
    }

    [Fact]
    public void Parse_ReturnsCanvasPaintingWithoutId_IfCanvasIdNotRecognised()
        => sut.ParseToCanvasPainting(new PresentationManifest
        {
            Items = [new Canvas { Id = "https://unrecognized.host/2/canvases/foo" }]
        }, [], 2).Single().Id.Should().BeNull();
    
    [Fact]
    public async Task Parse_Throws_MissingBody()
    {
        // Arrange
        var manifest = """

                       {
                           "@context": "http://iiif.io/api/presentation/3/context.json",
                           "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json",
                           "type": "Manifest",
                           "items": [
                               {
                                   "id": "i-am-not-a-uri",
                                   "type": "Canvas",
                                   "height": 1800,
                                   "width": 1200,
                                   "items": [
                                       {
                                           "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1",
                                           "type": "AnnotationPage",
                                           "items": [
                                               {
                                                   "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image",
                                                   "type": "Annotation",
                                                   "motivation": "painting",
                                                   "target": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1"
                                               }
                                           ]
                                       }
                                   ]
                               }
                           ]
                       }
                       """;
        
        var deserialised = await manifest.ToPresentation<PresentationManifest>();
         
        // Act
        Action action = () => sut.ParseToCanvasPainting(deserialised, [], 123, EmptyRecognizedDictionary);
        
        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Body type '' not supported as painting annotation body");
    }
    
    [Fact]
    public async Task Parse_SingleImage()
    {
        // https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json
        // Arrange
        var manifest = """

                       {
                           "@context": "http://iiif.io/api/presentation/3/context.json",
                           "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json",
                           "type": "Manifest",
                           "items": [
                               {
                                   "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1",
                                   "type": "Canvas",
                                   "height": 1800,
                                   "width": 1200,
                                   "items": [
                                       {
                                           "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1",
                                           "type": "AnnotationPage",
                                           "items": [
                                               {
                                                   "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image",
                                                   "type": "Annotation",
                                                   "motivation": "painting",
                                                   "body": {
                                                       "id": "http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png",
                                                       "type": "Image",
                                                       "format": "image/png",
                                                       "height": 1800,
                                                       "width": 1200
                                                   },
                                                   "target": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1"
                                               }
                                           ]
                                       }
                                   ]
                               }
                           ]
                       }
                       """;
        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1"),
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 0,
                ChoiceOrder = null,
                Target = null
            }
        };
        
        var deserialised = await manifest.ToPresentation<PresentationManifest>();
         
        // Act
        var canvasPaintings = sut.ParseToCanvasPainting(deserialised, [], 123, EmptyRecognizedDictionary);
        
        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task Parse_SingleImage_WithThumbnail()
    {
        // https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json (with added thumbnail)
        // Arrange
        var manifest = """

                       {
                           "@context": "http://iiif.io/api/presentation/3/context.json",
                               "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json",
                                   "type": "Manifest",
                                       "label": {
                               "en": [
                                   "Single Image Example"
                               ]
                           },
                           "items": [
                               {
                                   "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1",
                                   "type": "Canvas",
                                   "height": 1800,
                                   "width": 1200,
                                   "thumbnail": [
                                       {
                                           "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/image-example/full/648,1024/0/default.jpg",
                                           "type": "Image",
                                           "format": "image/jpeg",
                                           "service": [
                                               {
                                                   "@context": "http://iiif.io/api/image/3/context.json",
                                                   "id": "https://dlc.services/thumbs/v3/6/1/5c084b27-cdd8-4c8d-b1b2-e3cc3f4155bb",
                                                   "type": "ImageService3",
                                                   "profile": "level0",
                                                   "sizes": [
                                                       {
                                                           "width": 648,
                                                           "height": 1024
                                                       },
                                                       {
                                                           "width": 253,
                                                           "height": 400
                                                       },
                                                       {
                                                           "width": 127,
                                                           "height": 200
                                                       },
                                                       {
                                                           "width": 63,
                                                           "height": 100
                                                       }
                                                   ]
                                               }
                                           ]
                                       }
                                   ],
                                   "items": [
                                       {
                                           "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1",
                                           "type": "AnnotationPage",
                                           "items": [
                                               {
                                                   "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image",
                                                   "type": "Annotation",
                                                   "motivation": "painting",
                                                   "body": {
                                                       "id": "http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png",
                                                       "type": "Image",
                                                       "format": "image/png",
                                                       "height": 1800,
                                                       "width": 1200
                                                   },
                                                   "target": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1"
                                               }
                                           ]
                                       }
                                   ]
                               }
                           ]
                       }

                       """;
        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1"),
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 0,
                ChoiceOrder = null,
                Target = null,
                Thumbnail = new Uri("https://iiif.io/api/cookbook/recipe/0001-mvm-image/image-example/full/648,1024/0/default.jpg")
            }
        };
        
        var deserialised = await manifest.ToPresentation<PresentationManifest>();
         
        // Act
        var canvasPaintings = sut.ParseToCanvasPainting(deserialised, [], 123, EmptyRecognizedDictionary);
        
        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task Parse_SingleImage_RecognizedAsset()
    {
        // Arrange
        var manifest = """

                       {
                           "@context": "http://iiif.io/api/presentation/3/context.json",
                           "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json",
                           "type": "Manifest",
                           "items": [
                               {
                                   "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1",
                                   "type": "Canvas",
                                   "height": 1800,
                                   "width": 1200,
                                   "items": [
                                       {
                                           "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1",
                                           "type": "AnnotationPage",
                                           "items": [
                                               {
                                                   "id": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image",
                                                   "type": "Annotation",
                                                   "motivation": "painting",
                                                   "body": {
                                                       "id": "http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png",
                                                       "type": "Image",
                                                       "format": "image/png",
                                                       "height": 1800,
                                                       "width": 1200
                                                   },
                                                   "target": "https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1"
                                               }
                                           ]
                                       }
                                   ]
                               }
                           ]
                       }
                       """;
        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1"),
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 0,
                ChoiceOrder = null,
                Target = null,
                AssetId = new AssetId(123,1,"theAssetId")
            }
        };
        
        var deserialised = await manifest.ToPresentation<PresentationManifest>();
         
        // Note: this is manually replicating the behaviour impl elsewhere that is not performed in this test
        var onlyPaintableAnnotation = (PaintingAnnotation)deserialised!.Items![0].Items![0].Items![0];
        var onlyPaintable = onlyPaintableAnnotation.Body;

        // Note that the actual asset recognition is NOT tested here, hence the AssetId is constructed regardless of the
        // actual IPaintable's properties.
        Dictionary<IPaintable, AssetId> recognizedDictionary = new()
        {
            { onlyPaintable!, new AssetId(123, 1, "theAssetId") }
        };
        
        // Act
        var canvasPaintings = sut.ParseToCanvasPainting(deserialised, 123, recognizedDictionary);

        
        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task Parse_SingleSound()
    {
        // https://iiif.io/api/cookbook/recipe/0002-mvm-audio/manifest.json
        // Arrange
        var manifest = """

                       {
                           "@context": "http://iiif.io/api/presentation/3/context.json",
                           "id": "https://iiif.io/api/cookbook/recipe/0002-mvm-audio/manifest.json",
                           "type": "Manifest",
                           "label": {
                               "en": [
                                   "Simplest Audio Example 1"
                               ]
                           },
                           "items": [
                               {
                                   "id": "https://iiif.io/api/cookbook/recipe/0002-mvm-audio/canvas",
                                   "type": "Canvas",
                                   "duration": 1985.024,
                                   "items": [
                                       {
                                           "id": "https://iiif.io/api/cookbook/recipe/0002-mvm-audio/canvas/page",
                                           "type": "AnnotationPage",
                                           "items": [
                                               {
                                                   "id": "https://iiif.io/api/cookbook/recipe/0002-mvm-audio/canvas/page/annotation",
                                                   "type": "Annotation",
                                                   "motivation": "painting",
                                                   "body": {
                                                       "id": "https://fixtures.iiif.io/audio/indiana/mahler-symphony-3/CD1/medium/128Kbps.mp4",
                                                       "type": "Sound",
                                                       "format": "audio/mp4",
                                                       "duration": 1985.024
                                                   },
                                                   "target": "https://iiif.io/api/cookbook/recipe/0002-mvm-audio/canvas"
                                               }
                                           ]
                                       }
                                   ]
                               }
                           ]
                       }
                       """;
        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0002-mvm-audio/canvas"),
                StaticWidth = null,
                StaticHeight = null,
                CanvasOrder = 0,
                ChoiceOrder = null,
                Target = null
            }
        };
        
        var deserialised = await manifest.ToPresentation<PresentationManifest>();
         
        // Act
        var canvasPaintings = sut.ParseToCanvasPainting(deserialised, [], 123, EmptyRecognizedDictionary);
        
        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task Parse_SingleVideo()
    {
        // https://iiif.io/api/cookbook/recipe/0002-mvm-audio/manifest.json
        // Arrange
        var manifest = """

                       {
                           "@context": "http://iiif.io/api/presentation/3/context.json",
                           "id": "https://iiif.io/api/cookbook/recipe/0003-mvm-video/manifest.json",
                           "type": "Manifest",
                           "label": {
                               "en": [
                                   "Video Example 3"
                               ]
                           },
                           "items": [
                               {
                                   "id": "https://iiif.io/api/cookbook/recipe/0003-mvm-video/canvas",
                                   "type": "Canvas",
                                   "height": 360,
                                   "width": 480,
                                   "duration": 572.034,
                                   "items": [
                                       {
                                           "id": "https://iiif.io/api/cookbook/recipe/0003-mvm-video/canvas/page",
                                           "type": "AnnotationPage",
                                           "items": [
                                               {
                                                   "id": "https://iiif.io/api/cookbook/recipe/0003-mvm-video/canvas/page/annotation",
                                                   "type": "Annotation",
                                                   "motivation": "painting",
                                                   "body": {
                                                       "id": "https://fixtures.iiif.io/video/indiana/lunchroom_manners/high/lunchroom_manners_1024kb.mp4",
                                                       "type": "Video",
                                                       "height": 360,
                                                       "width": 480,
                                                       "duration": 572.034,
                                                       "format": "video/mp4"
                                                   },
                                                   "target": "https://iiif.io/api/cookbook/recipe/0003-mvm-video/canvas"
                                               }
                                           ]
                                       }
                                   ]
                               }
                           ]
                       }

                       """;
        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0003-mvm-video/canvas"),
                StaticWidth = 480,
                StaticHeight = 360,
                CanvasOrder = 0,
                ChoiceOrder = null,
                Target = null
            }
        };
        
        var deserialised = await manifest.ToPresentation<PresentationManifest>();
         
        // Act
        var canvasPaintings = sut.ParseToCanvasPainting(deserialised, [], 123, EmptyRecognizedDictionary);
        
        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
}
