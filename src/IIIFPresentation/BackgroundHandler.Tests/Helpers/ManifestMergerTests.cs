using AWS.Settings;
using BackgroundHandler.Helpers;
using BackgroundHandler.Settings;
using BackgroundHandler.Tests.BatchCompletion;
using FluentAssertions;
using IIIF;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers;
using Canvas = IIIF.Presentation.V3.Canvas;
using Manifest = IIIF.Presentation.V3.Manifest;

namespace BackgroundHandler.Tests.Helpers;

public class ManifestMergerTests
{
    private readonly ManifestMerger sut;

    public ManifestMergerTests()
    {
        var backgroundHandlerSettings = new BackgroundHandlerSettings
        {
            PresentationApiUrl = new Uri("https://localhost:5000"),
            AWS = new AWSSettings(),
        };
        var presentationGenerator =
            new SettingsDrivenPresentationConfigGenerator(Options.Create(backgroundHandlerSettings));
        var pathGenerator = new TestPathGenerator(presentationGenerator);
        
        sut = new ManifestMerger(pathGenerator, new NullLogger<ManifestMerger>());
    }
    
    [Fact]
    public void ProcessCanvasPaintings_Throws_IfNQManifest_NoItems()
    {
        // Arrange
        var blankManifest = new Manifest();
        var namedQueryManifest = new Manifest();

        // Act
        Action action = () => sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, null);

        // Assert
        action.Should()
            .ThrowExactly<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'namedQueryManifest.Items')");
    }
    
    [Fact]
    public void ProcessCanvasPaintings_Throws_IfBaseManifest_HasItems()
    {
        // Arrange
        var blankManifest = new Manifest
        {
            Id = "https://foo",
            Items = [new Canvas()]
        };
        var namedQueryManifest = new Manifest { Items = [new Canvas()] };

        // Act
        Action action = () => sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, null);

        // Assert
        action.Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage("https://foo contains items. Generating manifest from paintedResources with items is not currently supported");
    }
    
    [Fact]
    public void ProcessCanvasPaintings_ReturnsBaseManifest_IfNoCanvasPaintings()
    {
        // Arrange
        var baseManifest = new Manifest
        {
            Id = "the-test-one",
            Metadata = [new("label1", "value1")],
            Rights = "https://invalid"
        };
        var assetId = TestIdentifiers.AssetId();

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithImage())
            .Build();
        
        // Act
        var finalManifest = sut.ProcessCanvasPaintings(baseManifest, namedQueryManifest, null);

        // Assert
        finalManifest.Should().Be(baseManifest);
    }
    
    [Fact]
    public void ProcessCanvasPaintings_MaintainsEverythingInBaseManifest()
    {
        // Arrange
        var baseManifest = new Manifest
        {
            Id = "https://foo",
            Metadata = [new("label1", "value1")],
            Rights = "http://rightsstatements.org/vocab/NKC/1.0/",
            Provider =
            [
                new Agent
                {
                    Id = "https://agent",
                    Label = new("label1", "value1"),
                    Homepage =
                    [
                        new ExternalResource("Text")
                        {
                            Id = "https://external-resource",
                            Format = "text/html"
                        }
                    ],
                    Logo =
                    [
                        new Image
                        {
                            Id = "https://logo.png",
                            Format = "image/png"
                        }
                    ]
                }
            ],
            SeeAlso =
            [
                new ExternalResource("DataSet")
                {
                    Id = "https://external-resource",
                    Format = "application/json"
                }
            ]
        };
        var assetId = TestIdentifiers.AssetId();

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithImage())
            .Build();

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(assetId);
        
        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(baseManifest, namedQueryManifest, canvasPaintings);

        // Assert
        mergedManifest.Should().BeEquivalentTo(baseManifest, options => options.Excluding(m => m.Items));
    }
    
    [Fact]
    public void ProcessCanvasPaintings_GeneratesExpectedManifest_SingleImage()
    {
        // Arrange
        var blankManifest = new Manifest();
        var assetId = TestIdentifiers.AssetId();

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithImage())
            .Build();

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(assetId);
        
        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        mergedManifest.Thumbnail.Should().BeNull("Thumbnail not defaulted with value from NQ");
        mergedManifest.Metadata.Should().BeNull("No manifest metadata from NQ persisted");
        mergedManifest.Items.Should().HaveCount(1, "Single canvasPainting");
        var canvas = mergedManifest.Items[0];
        canvas.Width.Should().Be(110, "Width from NQ");
        canvas.Height.Should().Be(110, "Height from NQ");
        canvas.Label.Should().ContainKey("canvasPaintingLabel", "Label from CanvasPainting");
        canvas.Metadata.Should().BeNull("No canvas metadata from NQ persisted");
    }

    [Fact]
    public void ProcessCanvasPaintings_GeneratesExpectedManifest_SingleImage_WithLabelAndCanvasLabel()
    {
        // Arrange
        var blankManifest = new Manifest();
        var assetId = TestIdentifiers.AssetId();

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithImage())
            .Build();
        namedQueryManifest.Items[0].Label = null;

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(assetId);
        var canvasLabel = new LanguageMap("canvasPaintingCanvasLabel", "Generated canvas painting label");
        canvasPaintings[0].CanvasLabel = canvasLabel;

        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        mergedManifest.Items.Should().HaveCount(1, "Single canvasPainting");
        var canvas = mergedManifest.Items[0];
        canvas.Label.Should().BeEquivalentTo(canvasLabel, "'CanvasLabel' used to label canvas");
        canvas.Items[0].GetFirstPaintingAnnotation().Body.As<Image>().Label.Should()
            .ContainKey("canvasPaintingLabel", "'Label' used to label Image as 'CanvasLabel' present");

    }

    [Fact]
    public void ProcessCanvasPaintings_ShouldNotUpdateAttachedManifestThumbnail()
    {
        // Arrange
        var assetId = TestIdentifiers.AssetId();
        var thumbnail = ManifestTestCreator.GenerateImageService("sample_id");
        var baseManifest = new Manifest
        {
            Id = "the-test-one",
            Metadata = [new("label1", "value1")],
            Rights = "https://invalid",
            Thumbnail = [thumbnail]
        };

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithImage())
            .Build();

        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(baseManifest, namedQueryManifest,
            ManifestTestCreator.GenerateCanvasPaintings(assetId));

        // Assert
        mergedManifest.Items.Should().HaveCount(1, "Single canvasPainting");
        mergedManifest.Thumbnail[0].Should().BeEquivalentTo(thumbnail, "Thumbnail on base manifest unchanged");
    }

    [Fact]
    public void ProcessCanvasPaintings_CorrectlyOrdersItemsAccordingToCanvasPainting()
    {
        // Arrange
        var blankManifest = new Manifest();
        var assetIdOne = TestIdentifiers.AssetId(postfix: "_1");
        var assetIdTwo = TestIdentifiers.AssetId(postfix: "_2");

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetIdOne, c => c.WithImage())
            .WithCanvas(assetIdTwo, c => c.WithImage())
            .Build();

        // NOTE - added as assetIdTwo, assetIdOne so that 'two' has lower CanvasOrder
        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(assetIdTwo, assetIdOne);

        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        mergedManifest.Items.Should().HaveCount(2, "Two canvas paintings");
        mergedManifest.Items[0].Id.Should().Contain(assetIdTwo.ToString(), "assetIdTwo had lower CanvasOrder");
        mergedManifest.Items[1].Id.Should().Contain(assetIdOne.ToString(), "assetIdOne had higher CanvasOrder");
    }

    [Fact]
    public void ProcessCanvasPaintings_CorrectlyArrangesItems_WithChoiceOrderWithNoThumbnail()
    {
        // Arrange
        var blankManifest = new Manifest();

        var canvas0Choice1 = TestIdentifiers.AssetId(postfix: "_1");
        var canvas1Choice2 = TestIdentifiers.AssetId(postfix: "_2");
        var canvas0Choice2 = TestIdentifiers.AssetId(postfix: "_3");
        var canvas1Choice1 = TestIdentifiers.AssetId(postfix: "_4");
        var canvas2NoChoice = TestIdentifiers.AssetId(postfix: "_5");

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(canvas0Choice1, c => c.WithImage())
            .WithCanvas(canvas1Choice2, c => c.WithImage())
            .WithCanvas(canvas0Choice2, c => c.WithImage())
            .WithCanvas(canvas1Choice1, c => c.WithImage())
            .WithCanvas(canvas2NoChoice, c => c.WithImage())
            .Build();
        
        namedQueryManifest.Items[0].Thumbnail = null;
        namedQueryManifest.Items[1].Thumbnail = null;
        namedQueryManifest.Items[2].Thumbnail = null;
        namedQueryManifest.Items[3].Thumbnail = null;
        namedQueryManifest.Items[4].Thumbnail = null;
        
        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(
            canvas0Choice1,
            canvas1Choice2,
            canvas0Choice2,
            canvas1Choice1,
            canvas2NoChoice
        );

        // First canvas, first choice
        canvasPaintings[0].Id = "first";
        canvasPaintings[0].CanvasOrder = 0;
        canvasPaintings[0].ChoiceOrder = 1;

        // Second canvas, second choice
        canvasPaintings[1].Id = "second";
        canvasPaintings[1].CanvasOrder = 1;
        canvasPaintings[1].ChoiceOrder = 2;
        
        // First canvas, second choice
        canvasPaintings[2].Id = "first";
        canvasPaintings[2].CanvasOrder = 0;
        canvasPaintings[2].ChoiceOrder = 2;

        // Second canvas, first choice
        canvasPaintings[3].Id = "second";
        canvasPaintings[3].CanvasOrder = 1;
        canvasPaintings[3].ChoiceOrder = 1;

        // Third canvas, no choice
        canvasPaintings[4].Id = "third";
        canvasPaintings[4].CanvasOrder = 2;
        canvasPaintings[4].ChoiceOrder = null;

        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        mergedManifest.Items.Should().HaveCount(3, "5 canvas paintings but 3 unique Ids");
        mergedManifest.Thumbnail.Should().BeNull();

        // should be 1 + 3 then 4 + 2 then 5
        mergedManifest.Items[0].Id.Should().Contain("first");
        mergedManifest.Items[0].Thumbnail.Should().BeNull("ManifestMerger handles no thumbs");
        mergedManifest.Items[1].Id.Should().Contain("second");
        mergedManifest.Items[1].Thumbnail.Should().BeNull("ManifestMerger handles no thumbs");
        mergedManifest.Items[2].Id.Should().Contain("third");
        mergedManifest.Items[2].Thumbnail.Should().BeNull("ManifestMerger handles no thumbs");
    }

    [Fact]
    public void ProcessCanvasPaintings_CorrectlyArrangesChoices()
    {
        // Arrange
        var blankManifest = new Manifest();

        var canvas0Choice1 = TestIdentifiers.AssetId(postfix: "_1");
        var canvas1Choice2 = TestIdentifiers.AssetId(postfix: "_2");
        var canvas0Choice2 = TestIdentifiers.AssetId(postfix: "_3");
        var canvas1Choice1 = TestIdentifiers.AssetId(postfix: "_4");
        var canvas2NoChoice = TestIdentifiers.AssetId(postfix: "_5");

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(canvas0Choice1, c => c.WithImage())
            .WithCanvas(canvas1Choice2, c => c.WithImage())
            .WithCanvas(canvas0Choice2, c => c.WithImage())
            .WithCanvas(canvas1Choice1, c => c.WithImage())
            .WithCanvas(canvas2NoChoice, c => c.WithImage())
            .Build();
        
        // Clear thumb from 3 in order that 2nd choice thumbnail is used to prove first non-null is used. Not just first
        namedQueryManifest.Items[3].Thumbnail = null;

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(
            canvas0Choice1,
            canvas1Choice2,
            canvas0Choice2,
            canvas1Choice1,
            canvas2NoChoice
        );

        // First canvas, first choice
        canvasPaintings[0].Id = "first";
        canvasPaintings[0].CanvasOrder = 0;
        canvasPaintings[0].ChoiceOrder = 1;
        canvasPaintings[0].CanvasLabel =
            new LanguageMap("canvas0Choice1CanvasLabel", "generated canvas painting label");

        // Second canvas, second choice
        canvasPaintings[1].Id = "second";
        canvasPaintings[1].CanvasOrder = 1;
        canvasPaintings[1].ChoiceOrder = 2;
        canvasPaintings[1].CanvasLabel =
            new LanguageMap("canvas1Choice1CanvasLabel", "generated canvas painting label");
        canvasPaintings[1].Label = null;
        
        // First canvas, second choice
        canvasPaintings[2].Id = "first";
        canvasPaintings[2].CanvasOrder = 0;
        canvasPaintings[2].ChoiceOrder = 2;

        // Second canvas, first choice
        canvasPaintings[3].Id = "second";
        canvasPaintings[3].CanvasOrder = 1;
        canvasPaintings[3].ChoiceOrder = 1;

        // Third canvas, no choice
        canvasPaintings[4].Id = "third";
        canvasPaintings[4].CanvasOrder = 2;
        canvasPaintings[4].ChoiceOrder = null;
        canvasPaintings[4].CanvasLabel = null;
        canvasPaintings[4].Label = null;

        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        mergedManifest.Items.Should().HaveCount(3, "5 canvas paintings but 3 unique Ids");
        mergedManifest.Thumbnail.Should().BeNull();

        // Assert first canvas (2 choices)
        var firstCanvas = mergedManifest.Items[0];
        firstCanvas.Id.Should().Be("https://localhost:5000/0/canvases/first", "canvasId correct");
        firstCanvas.Label.Keys.Should()
            .Contain("canvas0Choice1CanvasLabel", "First non-null canvasLabel in choice used");
        firstCanvas.Thumbnail.Single().Id.Should()
            .Contain(canvas0Choice1.ToString(), "Thumbnail of first item in choice used");

        // Assert second canvas (2 choices)
        var secondCanvas = mergedManifest.Items[1];
        secondCanvas.Id.Should().Be("https://localhost:5000/0/canvases/second", "canvasId correct");
        secondCanvas.Label.Keys.Should()
            .Contain("canvas1Choice1CanvasLabel", "First non-null canvasLabel in choice used");
        secondCanvas.Thumbnail.Single().Id.Should().Contain(canvas1Choice2.ToString(),
            "Thumbnail of second item in choice used as first is null");

        // Assert third canvas (single item)
        var thirdCanvas = mergedManifest.Items[2];
        thirdCanvas.Id.Should().Contain("https://localhost:5000/0/canvases/third", "canvasId correct");
        thirdCanvas.Label.Should().BeNull("Only label from CanvasPainting used");
        thirdCanvas.Thumbnail.Single().Id.Should().Contain(canvas2NoChoice.ToString(), "Thumbnail of single item used");

        var firstCanvasAnnotationPage = mergedManifest.GetCanvasAnnotationPage(0);
        firstCanvasAnnotationPage.Id.Should().Be("https://localhost:5000/0/canvases/first/annopages/0",
            "AnnoPage id based on canvas");

        var firstCanvasSinglePaintingAnno = firstCanvasAnnotationPage.GetFirstPaintingAnnotation()!;
        firstCanvasSinglePaintingAnno.Id.Should()
            .Be("https://localhost:5000/0/canvases/first/annotations/0", "Anno id based on canvas");

        var target = firstCanvasSinglePaintingAnno.Target as Canvas;
        target.Id.Should().Be("https://localhost:5000/0/canvases/first", "Painting anno targets entire canvas");

        var firstAnnotationBody = firstCanvasSinglePaintingAnno.Body.As<PaintingChoice>();
        firstAnnotationBody.Items[0].As<Image>().Id.Should().Contain(canvas0Choice1.ToString(), "Choices are in correct order");
        firstAnnotationBody.Items[0].As<Image>().Label.Keys.Should().Contain("canvasPaintingLabel", "Choice label set");
        firstAnnotationBody.Items[1].As<Image>().Id.Should().Contain(canvas0Choice2.ToString(), "Choices are in correct order");

        var secondAnnotationBody = mergedManifest.GetCanvasAnnotationPage(1).Items[0].As<PaintingAnnotation>()
            .Body.As<PaintingChoice>();
        secondAnnotationBody.Items[0].As<Image>().Id.Should().Contain(canvas1Choice1.ToString());
        secondAnnotationBody.Items[1].As<Image>().Id.Should().Contain(canvas1Choice2.ToString());
        secondAnnotationBody.Items[1].As<Image>().Label.Should()
            .BeNull("label cannot be carried over from named query");
    }
    
    [Fact]
    public void ProcessCanvasPaintings_CorrectlyArrangesCompositeCanvases()
    {
        // Arrange
        var blankManifest = new Manifest();

        // CanvasPainting will have 6 items on 2 canvases.
        // First canvas has 1 item.
        // Second canvas has 5 canvasPaintings across 4 painting-annos; 3 full Image assets and 2 in a choice 
        var canvas0 = TestIdentifiers.AssetId(postfix: "_1");
        var canvas1Background = TestIdentifiers.AssetId(postfix: "_2");
        var canvas1TopLeft = TestIdentifiers.AssetId(postfix: "_3");
        var canvas1BottomRight = TestIdentifiers.AssetId(postfix: "_4");
        var canvas1Choice1 = TestIdentifiers.AssetId(postfix: "_5");
        var canvas1Choice2 = TestIdentifiers.AssetId(postfix: "_6");

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(canvas0, c => c.WithImage())
            .WithCanvas(canvas1Background, c => c.WithImage())
            .WithCanvas(canvas1TopLeft, c => c.WithImage())
            .WithCanvas(canvas1BottomRight, c => c.WithImage())
            .WithCanvas(canvas1Choice1, c => c.WithImage())
            .WithCanvas(canvas1Choice2, c => c.WithImage())
            .Build();
        
        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(
            canvas0,
            canvas1Background,
            canvas1TopLeft,
            canvas1BottomRight,
            canvas1Choice1,
            canvas1Choice2
        );

        // First canvas, single item
        canvasPaintings[0].Id = "first";
        canvasPaintings[0].CanvasOrder = 0;
        canvasPaintings[0].CanvasLabel =
            new LanguageMap("canvas0CanvasLabel", "generated canvas painting label");
        canvasPaintings[0].Label =
            new LanguageMap("canvas0Label", "generated canvas painting label");

        // Second canvas, first item targetting entire canvas
        canvasPaintings[1].Id = "second";
        canvasPaintings[1].CanvasOrder = 1;
        canvasPaintings[1].Label = new LanguageMap("canvas1FirstLabel", "Background");
        
        // Second canvas, 2nd item targets top left via target
        canvasPaintings[2].Id = "second";
        canvasPaintings[2].CanvasOrder = 2;
        canvasPaintings[2].Target = "xywh=0,0,200,200";
        canvasPaintings[2].Label = new LanguageMap("canvas1SecondLabel", "Top Left");

        // Second canvas, first choice
        canvasPaintings[3].Id = "second";
        canvasPaintings[3].CanvasOrder = 3;
        canvasPaintings[3].Target = "xywh=200,200,200,200";
        canvasPaintings[3].Label = new LanguageMap("canvas1ThirdLabel", "Bottom Right");

        // Third canvas, no choice
        canvasPaintings[4].Id = "second";
        canvasPaintings[4].CanvasOrder = 4;
        canvasPaintings[4].ChoiceOrder = 1;
        canvasPaintings[4].Label = new LanguageMap("canvas1_1:0", "Bottom Right");
        
        canvasPaintings[5].Id = "second";
        canvasPaintings[5].CanvasOrder = 4;
        canvasPaintings[5].ChoiceOrder = 2;
        canvasPaintings[5].Target = "xywh=50,50,50,50";
        canvasPaintings[5].Label = null;
        canvasPaintings[5].CanvasLabel =
            new LanguageMap("canvas1CanvasLabel", "generated canvas painting label");

        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        mergedManifest.Items.Should().HaveCount(2, "6 canvas paintings but 2 unique canvas Ids");
        mergedManifest.Thumbnail.Should().BeNull();

        // Assert first canvas (single item choices)
        var firstCanvas = mergedManifest.Items[0];
        firstCanvas.Id.Should().Be("https://localhost:5000/0/canvases/first", "canvasId correct");
        firstCanvas.Label.Should()
            .ContainKey("canvas0CanvasLabel", "CanvasLabel used for canvas");
        firstCanvas.Thumbnail.Single().Id.Should()
            .Contain(canvas0.ToString(), "Thumbnail of first item in choice used");
        var paintingAnno = firstCanvas.GetFirstPaintingAnnotation();
        paintingAnno.Target.As<Canvas>().Id.Should().Be(firstCanvas.Id, "Targets entire canvas");
        paintingAnno.Body.As<Image>().Label.Should().ContainKey("canvas0Label", "Label used for body");
        
        // Assert second canvas (4 painting annos)
        var secondCanvas = mergedManifest.Items[1];
        secondCanvas.Rendering.Should().HaveCount(5, "Renderings are accumulated");
        secondCanvas.Id.Should().Be("https://localhost:5000/0/canvases/second", "canvasId correct");
        secondCanvas.Label.Keys.Should()
            .Contain("canvas1CanvasLabel", "First non-null canvasLabel in canvas is used");
        secondCanvas.Thumbnail.Single().Id.Should().Contain(canvas1Background.ToString(),
            "First non-null thumbnail used for canvas");
        
        secondCanvas.GetFirstAnnotationPage().Items.Should()
            .HaveCount(4, "5 canvasPaintings share canvas but 2 are choice");
        
        // First painting anno (background)
        var backgroundPaintingAnno = secondCanvas.GetPaintingAnno(0);
        backgroundPaintingAnno.Id.Should().Be("https://localhost:5000/0/canvases/second/annotations/1");
        backgroundPaintingAnno.Target.As<Canvas>().Id.Should().Be(secondCanvas.Id, "Targets entire canvas");
        backgroundPaintingAnno.Body.As<Image>().Label.Should().ContainKey("canvas1FirstLabel", "Has label");
        backgroundPaintingAnno.Body.As<Image>().Id.Should().Contain(canvas1Background.ToString());
        
        // Second painting anno (top-left)
        var topLeftPaintingAnno = secondCanvas.GetPaintingAnno(1);
        topLeftPaintingAnno.Id.Should().Be("https://localhost:5000/0/canvases/second/annotations/2");
        topLeftPaintingAnno.Target.As<Canvas>().Id.Should().Be($"{secondCanvas.Id}#xywh=0,0,200,200", "Targets section of canvas");
        topLeftPaintingAnno.Body.As<Image>().Label.Should().ContainKey("canvas1SecondLabel", "Has label");
        topLeftPaintingAnno.Body.As<Image>().Id.Should().Contain(canvas1TopLeft.ToString());

        // Third painting anno (bottom right)
        var bottomRightPaintingAnno = secondCanvas.GetPaintingAnno(2);
        bottomRightPaintingAnno.Id.Should().Be("https://localhost:5000/0/canvases/second/annotations/3");
        bottomRightPaintingAnno.Target.As<Canvas>().Id.Should().Be($"{secondCanvas.Id}#xywh=200,200,200,200", "Targets section of canvas");
        bottomRightPaintingAnno.Body.As<Image>().Label.Should().ContainKey("canvas1ThirdLabel", "Has label");
        bottomRightPaintingAnno.Body.As<Image>().Id.Should().Contain(canvas1BottomRight.ToString());
        
        // Fourth painting anno (choice)
        var choicePaintingAnno = secondCanvas.GetPaintingAnno(3);
        choicePaintingAnno.Id.Should().Be("https://localhost:5000/0/canvases/second/annotations/4");
        choicePaintingAnno.Target.As<Canvas>().Id.Should().Be($"{secondCanvas.Id}#xywh=50,50,50,50", "Targets section of canvas");
        var paintingChoice = choicePaintingAnno.Body.As<PaintingChoice>();
        paintingChoice.Items.Should().HaveCount(2);
        paintingChoice.Items[0].As<Image>().Label.Should().ContainKey("canvas1_1:0", "Label set on first choice");
        paintingChoice.Items[0].As<Image>().Id.Should().Contain(canvas1Choice1.ToString());
        paintingChoice.Items[1].As<Image>().Label.Should().BeNull("No label on second choice");
        paintingChoice.Items[1].As<Image>().Id.Should().Contain(canvas1Choice2.ToString());
    }
    
    [Fact]
    public void ProcessCanvasPaintings_IgnoresCanvasPaintings_IfAssetNotFound()
    {
        // Arrange
        var blankManifest = new Manifest();
        var assetIdFound = TestIdentifiers.AssetId();
        var assetIdNotFound = TestIdentifiers.AssetId(postfix: "_notfound");

        // Only 'assetIdFound' is in NQ result
        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetIdFound, c => c.WithImage())
            .Build();

        // NOTE - added as assetIdTwo, assetIdOne so that 'two' has lower CanvasOrder
        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(assetIdFound, assetIdNotFound);

        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        mergedManifest.Items.Should().HaveCount(2, "Two canvas paintings");
        mergedManifest.Items[1].GetFirstPaintingAnnotation().Body.Should().BeNull("No NQ painting anno found");
        
        var foundCanvasPainting = canvasPaintings.Single(cp => cp.AssetId == assetIdFound);
        foundCanvasPainting.Ingesting.Should().BeFalse("CP for asset found and marked as ingested");
        foundCanvasPainting.Thumbnail.Should().NotBeNull("Thumbnail set from NQ");
        
        canvasPaintings.Single(cp => cp.AssetId == assetIdNotFound).Ingesting.Should()
            .BeTrue("CP for asset not found therefor not marked as ingested");
    }

    [Fact]
    public void ProcessCanvasPaintings_UpdatesCanvasPaintings_WithSpatialDimensions()
    {
        var blankManifest = new Manifest();
        var assetId = TestIdentifiers.AssetId();

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithImage())
            .Build();

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(assetId);
        var canvasPainting = canvasPaintings.Single();

        // Act
        sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        canvasPainting.StaticWidth.Should().Be(100, "width taken from NQ manifest image->imageService");
        canvasPainting.StaticHeight.Should().Be(100, "height taken from NQ manifest image->imageService");
        canvasPainting.Modified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        canvasPainting.Ingesting.Should().BeFalse("CanvasPainting ingested");
    }
    
    [Fact]
    public void ProcessCanvasPaintings_UpdatesCanvasPaintings_WithTemporalDimensions()
    {
        var blankManifest = new Manifest();
        var assetId = TestIdentifiers.AssetId();

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithSound())
            .Build();

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(assetId);
        var canvasPainting = canvasPaintings.Single();

        // Act
        sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        canvasPainting.Duration.Should().Be(15000, "duration taken from NQ manifest sound");
        canvasPainting.Modified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        canvasPainting.Ingesting.Should().BeFalse("CanvasPainting ingested");
    }
    
    [Fact]
    public void ProcessCanvasPaintings_UpdatesCanvas_WithDimensions_IfStaticWidthAndHeight()
    {
        var blankManifest = new Manifest();
        var assetId = TestIdentifiers.AssetId();

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithImage())
            .Build();

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(assetId);
        canvasPaintings[0].StaticHeight = 200;
        canvasPaintings[0].StaticWidth = 220;

        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        var image = mergedManifest.Items[0].GetFirstPaintingAnnotation().Body as Image;
        image!.Id.Should()
            .Be($"https://dlcs.test/iiif-img/{assetId}/full/220,200/0/default.jpg",
                "w,h set from statics set on canvasPainting");
        image.Width.Should().Be(220, "width taken from statics set on canvasPainting");
        image.Height.Should().Be(200, "height taken from statics set on canvasPainting");
        canvasPaintings[0].Modified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        canvasPaintings[0].Ingesting.Should().BeFalse("CanvasPainting ingested");
    }
    
    [Fact]
    public void ProcessCanvasPaintings_UpdatesCanvas_WithDimensions_IfStaticWidthAndHeight_RewrittenPath()
    {
        var blankManifest = new Manifest();
        var assetId = TestIdentifiers.AssetId();

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithImage())
            .Build();
        
        // Fake the image.id like it has been rewritten
        var nqImage = namedQueryManifest.Items[0].GetFirstPaintingAnnotation().Body as Image;
        nqImage.Id = $"https://other.host/image/{assetId.Asset}/full/100,100/0/default.jpg";

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(assetId);
        canvasPaintings[0].StaticHeight = 200;
        canvasPaintings[0].StaticWidth = 220;

        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        var image = mergedManifest.Items[0].GetFirstPaintingAnnotation().Body as Image;
        image!.Id.Should()
            .Be($"https://other.host/image/{assetId.Asset}/full/220,200/0/default.jpg",
                "w,h set from statics set on canvasPainting");
        image.Width.Should().Be(220, "width taken from statics set on canvasPainting");
        image.Height.Should().Be(200, "height taken from statics set on canvasPainting");
        canvasPaintings[0].Modified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        canvasPaintings[0].Ingesting.Should().BeFalse("CanvasPainting ingested");
    }

    [Fact]
    public void ProcessCanvasPaintings_PreservesNonStandardContext()
    {
        var blankManifest = new Manifest();
        var assetId = TestIdentifiers.AssetId();

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithImage())
            .Build();
        const string nonStandardContext = "https://iiif.wellcomecollection.org/extensions/born-digital/context.json";
        namedQueryManifest.EnsureContext(nonStandardContext);
        
        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(assetId);

        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        mergedManifest.Context.As<string>().Should()
            .Be(nonStandardContext, "non standard context from NQ maintained");
    }
}
