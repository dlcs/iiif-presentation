using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging.Abstractions;
using Models.Database;
using Repository.Manifests;

namespace Repository.Tests.Manifests;

public class ManifestItemsParserTests
{
    private readonly ManifestItemsParser sut = new(new NullLogger<ManifestItemsParser>());

    [Fact]
    public void Parse_ReturnsEmptyEnumerable_IfItemsNull()
        => sut.ParseItemsToCanvasPainting(new Manifest()).Should().BeEmpty();

    [Fact]
    public void Parse_ReturnsEmptyEnumerable_IfItemsEmpty()
        => sut.ParseItemsToCanvasPainting(new Manifest { Items = [] }).Should().BeEmpty();

    [Fact]
    public void Parse_ReturnsEmptyEnumerable_IfCanvasHasNoAnnotationPages()
        => sut.ParseItemsToCanvasPainting(new Manifest { Items = [new Canvas { Items = [] }] }).Should().BeEmpty();

    [Fact]
    public void Parse_ReturnsEmptyEnumerable_IfAnnotationPagesHaveNoAnnotations()
        => sut.ParseItemsToCanvasPainting(new Manifest { Items = [new Canvas { Items = [new AnnotationPage()] }] })
            .Should().BeEmpty();

    [Fact]
    public void Parse_ReturnsEmptyEnumerable_IfAnnotationPagesHaveOnlyNonPaintingAnnotation()
        => sut.ParseItemsToCanvasPainting(new Manifest
            {
                Items = [new Canvas { Items = [new AnnotationPage { Items = [new TypeClassifyingAnnotation()] }] }]
            })
            .Should().BeEmpty();

    [Fact]
    public void Parse_Throws_CanvasIdInvalidUri()
    {
        // Arrange
        var manifest = @"
{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json"",
    ""type"": ""Manifest"",
    ""items"": [
        {
            ""id"": ""i-am-not-a-uri"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1800,
                                ""width"": 1200
                            },
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }
                    ]
                }
            ]
        }
    ]
}";
        
        var deserialised = manifest.FromJson<Manifest>();
         
        // Act
        Action action = () => sut.ParseItemsToCanvasPainting(deserialised);
        
        // Assert
        action.Should().Throw<UriFormatException>();
    }
    
    [Fact]
    public void Parse_Throws_MissingBody()
    {
        // Arrange
        var manifest = @"
{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json"",
    ""type"": ""Manifest"",
    ""items"": [
        {
            ""id"": ""i-am-not-a-uri"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }
                    ]
                }
            ]
        }
    ]
}";
        
        var deserialised = manifest.FromJson<Manifest>();
         
        // Act
        Action action = () => sut.ParseItemsToCanvasPainting(deserialised);
        
        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Body type '' not supported as painting annotation body");
    }
    
    [Fact]
    public void Parse_SingleImage()
    {
        // https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json
        // Arrange
        var manifest = @"
{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json"",
    ""type"": ""Manifest"",
    ""items"": [
        {
            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1800,
                                ""width"": 1200
                            },
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }
                    ]
                }
            ]
        }
    ]
}";
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
        
        var deserialised = manifest.FromJson<Manifest>();
         
        // Act
        var canvasPaintings = sut.ParseItemsToCanvasPainting(deserialised);
        
        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Parse_SingleImage_WithThumbnail()
    {
        // https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json (with added thumbnail)
        // Arrange
        var manifest = @"
{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
        ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json"",
            ""type"": ""Manifest"",
                ""label"": {
        ""en"": [
            ""Single Image Example""
        ]
    },
    ""items"": [
        {
            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""thumbnail"": [
                {
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/image-example/full/648,1024/0/default.jpg"",
                    ""type"": ""Image"",
                    ""format"": ""image/jpeg"",
                    ""service"": [
                        {
                            ""@context"": ""http://iiif.io/api/image/3/context.json"",
                            ""id"": ""https://dlc.services/thumbs/v3/6/1/5c084b27-cdd8-4c8d-b1b2-e3cc3f4155bb"",
                            ""type"": ""ImageService3"",
                            ""profile"": ""level0"",
                            ""sizes"": [
                                {
                                    ""width"": 648,
                                    ""height"": 1024
                                },
                                {
                                    ""width"": 253,
                                    ""height"": 400
                                },
                                {
                                    ""width"": 127,
                                    ""height"": 200
                                },
                                {
                                    ""width"": 63,
                                    ""height"": 100
                                }
                            ]
                        }
                    ]
                }
            ],
            ""items"": [
                {
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1800,
                                ""width"": 1200
                            },
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }
                    ]
                }
            ]
        }
    ]
}
";
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
        
        var deserialised = manifest.FromJson<Manifest>();
         
        // Act
        var canvasPaintings = sut.ParseItemsToCanvasPainting(deserialised);
        
        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Parse_SingleSound()
    {
        // https://iiif.io/api/cookbook/recipe/0002-mvm-audio/manifest.json
        // Arrange
        var manifest = @"
{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.io/api/cookbook/recipe/0002-mvm-audio/manifest.json"",
    ""type"": ""Manifest"",
    ""label"": {
        ""en"": [
            ""Simplest Audio Example 1""
        ]
    },
    ""items"": [
        {
            ""id"": ""https://iiif.io/api/cookbook/recipe/0002-mvm-audio/canvas"",
            ""type"": ""Canvas"",
            ""duration"": 1985.024,
            ""items"": [
                {
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0002-mvm-audio/canvas/page"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0002-mvm-audio/canvas/page/annotation"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {
                                ""id"": ""https://fixtures.iiif.io/audio/indiana/mahler-symphony-3/CD1/medium/128Kbps.mp4"",
                                ""type"": ""Sound"",
                                ""format"": ""audio/mp4"",
                                ""duration"": 1985.024
                            },
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0002-mvm-audio/canvas""
                        }
                    ]
                }
            ]
        }
    ]
}";
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
        
        var deserialised = manifest.FromJson<Manifest>();
         
        // Act
        var canvasPaintings = sut.ParseItemsToCanvasPainting(deserialised);
        
        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Parse_SingleVideo()
    {
        // https://iiif.io/api/cookbook/recipe/0002-mvm-audio/manifest.json
        // Arrange
        var manifest = @"
{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.io/api/cookbook/recipe/0003-mvm-video/manifest.json"",
    ""type"": ""Manifest"",
    ""label"": {
        ""en"": [
            ""Video Example 3""
        ]
    },
    ""items"": [
        {
            ""id"": ""https://iiif.io/api/cookbook/recipe/0003-mvm-video/canvas"",
            ""type"": ""Canvas"",
            ""height"": 360,
            ""width"": 480,
            ""duration"": 572.034,
            ""items"": [
                {
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0003-mvm-video/canvas/page"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0003-mvm-video/canvas/page/annotation"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {
                                ""id"": ""https://fixtures.iiif.io/video/indiana/lunchroom_manners/high/lunchroom_manners_1024kb.mp4"",
                                ""type"": ""Video"",
                                ""height"": 360,
                                ""width"": 480,
                                ""duration"": 572.034,
                                ""format"": ""video/mp4""
                            },
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0003-mvm-video/canvas""
                        }
                    ]
                }
            ]
        }
    ]
}
";
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
        
        var deserialised = manifest.FromJson<Manifest>();
         
        // Act
        var canvasPaintings = sut.ParseItemsToCanvasPainting(deserialised);
        
        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Parse_SingleCanvas_WithChoices()
    {
        // https://iiif.io/api/cookbook/recipe/0033-choice/manifest.json (with some small changes)
        // Arrange
        var manifest = @"
{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.io/api/cookbook/recipe/0033-choice/manifest.json"",
    ""type"": ""Manifest"",
    ""label"": {
        ""en"": [
            ""John Dee performing an experiment before Queen Elizabeth I.""
        ]
    },
    ""items"": [
        {
            ""id"": ""https://iiif.io/api/cookbook/recipe/0033-choice/canvas/p1"",
            ""type"": ""Canvas"",
            ""height"": 1271,
            ""width"": 2000,
            ""items"": [
                {
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0033-choice/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0033-choice/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {
                                ""type"": ""Choice"",
                                ""items"": [
                                    {
                                        ""id"": ""https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-natural/full/max/0/default.jpg"",
                                        ""type"": ""Image"",
                                        ""format"": ""image/jpeg"",
                                        ""width"": 2000,
                                        ""height"": 1271,
                                        ""label"": {
                                            ""en"": [
                                                ""Natural Light""
                                            ]
                                        },
                                        ""service"": [
                                            {
                                                ""id"": ""https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-natural"",
                                                ""type"": ""ImageService3"",
                                                ""profile"": ""level1""
                                            }
                                        ]
                                    },
                                    {
                                        ""id"": ""https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-xray/full/max/0/default.jpg"",
                                        ""type"": ""Image"",
                                        ""format"": ""image/jpeg"",
                                        ""width"": 2001,
                                        ""height"": 1272,
                                        ""label"": {
                                            ""en"": [
                                                ""X-Ray""
                                            ]
                                        },
                                        ""service"": [
                                            {
                                                ""id"": ""https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-xray"",
                                                ""type"": ""ImageService3"",
                                                ""profile"": ""level1""
                                            }
                                        ]
                                    }
                                ]
                            },
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0033-choice/canvas/p1""
                        }
                    ]
                }
            ]
        }
    ]
}
";
        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0033-choice/canvas/p1"),
                StaticWidth = 2000,
                StaticHeight = 1271,
                CanvasOrder = 0,
                ChoiceOrder = 1,
                Target = null,
                Label = new LanguageMap("en", "Natural Light"),
            },
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0033-choice/canvas/p1"),
                StaticWidth = 2001,
                StaticHeight = 1272,
                CanvasOrder = 0,
                ChoiceOrder = 2,
                Target = null,
                Label = new LanguageMap("en", "X-Ray"),
            }
        };
        
        var deserialised = manifest.FromJson<Manifest>();
         
        // Act
        var canvasPaintings = sut.ParseItemsToCanvasPainting(deserialised);
        
        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Parse_MultiImageComposition()
    {
        // https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/manifest.json
        // Arrange
        var manifest = @"
{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/manifest.json"",
    ""type"": ""Manifest"",
    ""label"": {
        ""en"": [
            ""Folio from Grandes Chroniques de France, ca. 1460""
        ]
    },
    ""items"": [
        {
            ""id"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1"",
            ""type"": ""Canvas"",
            ""label"": {
                ""none"": [
                    ""f. 033v-034r [Chilpéric Ier tue Galswinthe, se remarie et est assassiné]""
                ]
            },
            ""height"": 5412,
            ""width"": 7216,
            ""items"": [
                {
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {
                                ""id"": ""https://iiif.io/api/image/3.0/example/reference/899da506920824588764bc12b10fc800-bnf_chateauroux/full/max/0/default.jpg"",
                                ""type"": ""Image"",
                                ""format"": ""image/jpeg"",
                                ""height"": 5412,
                                ""width"": 7216,
                                ""service"": [
                                    {
                                        ""id"": ""https://iiif.io/api/image/3.0/example/reference/899da506920824588764bc12b10fc800-bnf_chateauroux"",
                                        ""type"": ""ImageService3"",
                                        ""profile"": ""level1""
                                    }
                                ]
                            },
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1""
                        },
                        {
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/annotation/p0002-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {
                                ""id"": ""https://iiif.io/api/image/3.0/example/reference/899da506920824588764bc12b10fc800-bnf_chateauroux_miniature/full/max/0/default.jpg"",
                                ""type"": ""Image"",
                                ""format"": ""image/jpeg"",
                                ""label"": {
                                    ""fr"": [
                                        ""Miniature [Chilpéric Ier tue Galswinthe, se remarie et est assassiné]""
                                    ]
                                },
                                ""width"": 2138,
                                ""height"": 2414,
                                ""service"": [
                                    {
                                        ""id"": ""https://iiif.io/api/image/3.0/example/reference/899da506920824588764bc12b10fc800-bnf_chateauroux_miniature"",
                                        ""type"": ""ImageService3"",
                                        ""profile"": ""level1""
                                    }
                                ]
                            },
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1#xywh=3949,994,1091,1232""
                        }
                    ]
                }
            ]
        }
    ]
}
";
        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1"),
                StaticWidth = 7216,
                StaticHeight = 5412,
                CanvasOrder = 0,
                ChoiceOrder = null,
                Target = null,
                Label = new LanguageMap("none", "f. 033v-034r [Chilpéric Ier tue Galswinthe, se remarie et est assassiné]"),
            },
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1"),
                StaticWidth = 2138,
                StaticHeight = 2414,
                CanvasOrder = 1,
                ChoiceOrder = null,
                Target = "https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1#xywh=3949,994,1091,1232",
                Label = new LanguageMap("fr", "Miniature [Chilpéric Ier tue Galswinthe, se remarie et est assassiné]"),
                CanvasLabel = new LanguageMap("none", "f. 033v-034r [Chilpéric Ier tue Galswinthe, se remarie et est assassiné]"),
            }
        };

        var deserialised = manifest.FromJson<Manifest>();

        // Act
        var canvasPaintings = sut.ParseItemsToCanvasPainting(deserialised);

        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Parse_SpatialRegion()
    {
        // https://iiif.io/api/cookbook/recipe/0299-region/
        // Arrange
        var manifest = @"
{
  ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
  ""id"": ""https://iiif.io/api/cookbook/recipe/0299-region/manifest.json"",
  ""type"": ""Manifest"",
  ""label"": {
    ""en"": [
      ""Berliner Tageblatt article, 'Ein neuer Sicherungsplan?'""
    ]
  },
  ""items"": [
    {
      ""id"": ""https://iiif.io/api/cookbook/recipe/0299-region/canvas/p1"",
      ""type"": ""Canvas"",
      ""height"": 2080,
      ""width"": 1768,
      ""items"": [
        {
          ""id"": ""https://iiif.io/api/cookbook/recipe/0299-region/page/p1/1"",
          ""type"": ""AnnotationPage"",
          ""items"": [
            {
              ""id"": ""https://iiif.io/api/cookbook/recipe/0299-region/annotation/p0001-image"",
              ""type"": ""Annotation"",
              ""motivation"": ""painting"",
              ""body"": {
                ""id"": ""https://iiif.io/api/cookbook/recipe/0299-region/body/b1"",
                ""type"": ""SpecificResource"",
                ""source"": {
                  ""id"": ""https://iiif.io/api/image/3.0/example/reference/4ce82cef49fb16798f4c2440307c3d6f-newspaper-p2/full/max/0/default.jpg"",
                  ""type"": ""Image"",
                  ""format"": ""image/jpeg"",
                  ""height"": 4999,
                  ""width"": 3536,
                  ""service"": [
                    {
                      ""id"": ""https://iiif.io/api/image/3.0/example/reference/4ce82cef49fb16798f4c2440307c3d6f-newspaper-p2"",
                      ""profile"": ""level1"",
                      ""type"": ""ImageService3""
                    }
                  ]
                },
                ""selector"": {
                  ""type"": ""ImageApiSelector"",
                  ""region"": ""1768,2423,1768,2080""
                }
              },
              ""target"": ""https://iiif.io/api/cookbook/recipe/0299-region/canvas/p1""
            }
          ]
        }
      ]
    }
  ]
}
";
        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0299-region/canvas/p1"),
                StaticWidth = 3536,
                StaticHeight = 4999,
                CanvasOrder = 0,
                ChoiceOrder = null,
                Target = null,
            },
        };

        var deserialised = manifest.FromJson<Manifest>();

        // Act
        var canvasPaintings = sut.ParseItemsToCanvasPainting(deserialised);

        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Parse_MultiImageCompositionAndChoice()
    {
        // Based on https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/manifest.json
        // and https://iiif.io/api/cookbook/recipe/0033-choice/manifest.json
        
        // Arrange
        var manifest = @"
{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/manifest.json"",
    ""type"": ""Manifest"",
    ""label"": {
        ""en"": [
            ""Folio from Grandes Chroniques de France, ca. 1460""
        ]
    },
    ""items"": [
        {
            ""id"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1"",
            ""type"": ""Canvas"",
            ""label"": {
                ""none"": [
                    ""f. 033v-034r [Chilpéric Ier tue Galswinthe, se remarie et est assassiné]""
                ]
            },
            ""height"": 5412,
            ""width"": 7216,
            ""items"": [
                {
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {
                                ""id"": ""https://iiif.io/api/image/3.0/example/reference/899da506920824588764bc12b10fc800-bnf_chateauroux/full/max/0/default.jpg"",
                                ""type"": ""Image"",
                                ""format"": ""image/jpeg"",
                                ""height"": 5412,
                                ""width"": 7216,
                                ""service"": [
                                    {
                                        ""id"": ""https://iiif.io/api/image/3.0/example/reference/899da506920824588764bc12b10fc800-bnf_chateauroux"",
                                        ""type"": ""ImageService3"",
                                        ""profile"": ""level1""
                                    }
                                ]
                            },
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1""
                        },
                        {
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/annotation/p0002-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {
                                ""id"": ""https://iiif.io/api/image/3.0/example/reference/899da506920824588764bc12b10fc800-bnf_chateauroux_miniature/full/max/0/default.jpg"",
                                ""type"": ""Image"",
                                ""format"": ""image/jpeg"",
                                ""label"": {
                                    ""fr"": [
                                        ""Miniature [Chilpéric Ier tue Galswinthe, se remarie et est assassiné]""
                                    ]
                                },
                                ""width"": 2138,
                                ""height"": 2414,
                                ""service"": [
                                    {
                                        ""id"": ""https://iiif.io/api/image/3.0/example/reference/899da506920824588764bc12b10fc800-bnf_chateauroux_miniature"",
                                        ""type"": ""ImageService3"",
                                        ""profile"": ""level1""
                                    }
                                ]
                            },
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1#xywh=3949,994,1091,1232""
                        },
                        {
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0033-choice/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {
                                ""type"": ""Choice"",
                                ""items"": [
                                    {
                                        ""id"": ""https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-natural/full/max/0/default.jpg"",
                                        ""type"": ""Image"",
                                        ""format"": ""image/jpeg"",
                                        ""width"": 2000,
                                        ""height"": 1271,
                                        ""label"": {
                                            ""en"": [
                                                ""Natural Light""
                                            ]
                                        },
                                        ""service"": [
                                            {
                                                ""id"": ""https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-natural"",
                                                ""type"": ""ImageService3"",
                                                ""profile"": ""level1""
                                            }
                                        ]
                                    },
                                    {
                                        ""id"": ""https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-xray/full/max/0/default.jpg"",
                                        ""type"": ""Image"",
                                        ""format"": ""image/jpeg"",
                                        ""width"": 2001,
                                        ""height"": 1272,
                                        ""label"": {
                                            ""en"": [
                                                ""X-Ray""
                                            ]
                                        },
                                        ""service"": [
                                            {
                                                ""id"": ""https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-xray"",
                                                ""type"": ""ImageService3"",
                                                ""profile"": ""level1""
                                            }
                                        ]
                                    }
                                ]
                            },
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1#xywh=0,0,1091,1232""
                        }
                    ]
                }
            ]
        }
    ]
}
";
        var expected = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1"),
                StaticWidth = 7216,
                StaticHeight = 5412,
                CanvasOrder = 0,
                ChoiceOrder = null,
                Target = null,
                Label = new LanguageMap("none", "f. 033v-034r [Chilpéric Ier tue Galswinthe, se remarie et est assassiné]"),
            },
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1"),
                StaticWidth = 2138,
                StaticHeight = 2414,
                CanvasOrder = 1,
                ChoiceOrder = null,
                Target = "https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1#xywh=3949,994,1091,1232",
                Label = new LanguageMap("fr", "Miniature [Chilpéric Ier tue Galswinthe, se remarie et est assassiné]"),
                CanvasLabel = new LanguageMap("none", "f. 033v-034r [Chilpéric Ier tue Galswinthe, se remarie et est assassiné]"),
            },
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1"),
                StaticWidth = 2000,
                StaticHeight = 1271,
                CanvasOrder = 2,
                ChoiceOrder = 1,
                Target = "https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1#xywh=0,0,1091,1232",
                Label = new LanguageMap("en", "Natural Light"),
            },
            new()
            {
                CanvasOriginalId = new Uri("https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1"),
                StaticWidth = 2001,
                StaticHeight = 1272,
                CanvasOrder = 2,
                ChoiceOrder = 2,
                Target = null,
                Label = new LanguageMap("en", "X-Ray"),
            }
        };

        var deserialised = manifest.FromJson<Manifest>();

        // Act
        var canvasPaintings = sut.ParseItemsToCanvasPainting(deserialised);

        // Assert
        canvasPaintings.Should().BeEquivalentTo(expected);
    }
}
