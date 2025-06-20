using Core.Web;
using FluentAssertions;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Manifests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers;
using Test.Helpers.Helpers;
using Xunit;

namespace Manifests.Tests.Helpers;

public class ManifestMergerMixedContentTests
{
    private readonly ManifestMerger sut;

    public ManifestMergerMixedContentTests()
    {
        var presentationGenerator =
            new TestPresentationConfigGenerator("https://localhost:5000", new TypedPathTemplateOptions());
        var pathGenerator = new TestPathGenerator(presentationGenerator);
        
        sut = new ManifestMerger(pathGenerator, new NullLogger<ManifestMerger>());
    }
    
    [Fact]
    public void ProcessCanvasPaintings_GeneratesExpectedManifest_SingleVideo()
    {
        // Arrange
        var blankManifest = new Manifest();
        var assetId = TestIdentifiers.AssetId();

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithVideo())
            .Build();

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings(assetId);

        // Act
        var mergedManifest = sut.ProcessCanvasPaintings(blankManifest, namedQueryManifest, canvasPaintings);

        // Assert
        mergedManifest.Items.Should().HaveCount(1);
        var canvas = mergedManifest.Items![0];
        canvas.GetFirstPaintingAnnotation()!.Body.Should().BeOfType<Video>();
        canvas.Width.Should().Be(110, "Width from NQ");
        canvas.Height.Should().Be(110, "Height from NQ");
        canvas.Duration.Should().Be(15000, "Duration from NQ");
        canvas.Label.Should().ContainKey("canvasPaintingLabel", "Label from CanvasPainting");
        canvas.Metadata.Should().BeNull("No canvas metadata from NQ persisted");
    }

    [Fact]
    public void Merge_MixedContent_CorrectlyOrdersItemsWithChoiceOrder()
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
            .WithCanvas(canvas1Choice2, c => c.WithSounds(2))
            .WithCanvas(canvas0Choice2, c => c.WithImage())
            .WithCanvas(canvas1Choice1, c => c.WithSound())
            .WithCanvas(canvas2NoChoice, c => c.WithVideo())
            .Build();

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
            new("canvas0Choice1CanvasLabel", "generated canvas painting label");

        // Second canvas, second choice
        canvasPaintings[1].Id = "second";
        canvasPaintings[1].CanvasOrder = 1;
        canvasPaintings[1].ChoiceOrder = 2;
        // make sure canvas label isn't used and label isn't set for canvas1Choice0
        canvasPaintings[1].CanvasLabel =
            new("canvas1Choice1CanvasLabel", "generated canvas painting label");
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

        // remove labels from canvas2NoChoice
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
        firstCanvas.Thumbnail[0].Id.Should()
            .Contain(canvas0Choice1.ToString(), "Thumbnail of first item in choice used");

        // Assert second canvas (2 choices)
        var secondCanvas = mergedManifest.Items[1];
        secondCanvas.Id.Should().Be("https://localhost:5000/0/canvases/second", "canvasId correct");
        secondCanvas.Label.Keys.Should()
            .Contain("canvas1Choice1CanvasLabel", "First non-null canvasLabel in choice used");
        secondCanvas.Thumbnail[0].Id.Should().Contain(canvas1Choice1.ToString());


        var thirdCanvas = mergedManifest.Items[2];
        thirdCanvas.Id.Should().Be("https://localhost:5000/0/canvases/third", "canvasId correct");
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
        firstAnnotationBody.Items[0].As<Image>().Id.Should()
            .Contain(canvas0Choice1.ToString(), "Choices are in correct order");
        firstAnnotationBody.Items[0].As<Image>().Label.Keys.Should().Contain("canvasPaintingLabel", "Choice label set");
        firstAnnotationBody.Items[1].As<Image>().Id.Should()
            .Contain(canvas0Choice2.ToString(), "Choices are in correct order");

        var secondAnnotationBody = mergedManifest.GetCanvasAnnotationPage(1).Items[0].As<PaintingAnnotation>()
            .Body.As<PaintingChoice>();
        secondAnnotationBody.Items.Should()
            .HaveCount(3, "2 CanvasPaintings but 2nd returns a choice, which is flattened");
        secondAnnotationBody.Items[0].As<Sound>().Id.Should().Contain(canvas1Choice1.ToString());
        secondAnnotationBody.Items[1].As<Sound>().Id.Should().Contain(canvas1Choice2.ToString());
        secondAnnotationBody.Items[1].As<Sound>().Label.Should()
            .BeNull("label cannot be carried over from named query");
        secondAnnotationBody.Items[2].As<Sound>().Id.Should().Contain(canvas1Choice2.ToString());

        var thirdAnnotationBody = mergedManifest.GetCanvasAnnotationPage(2).Items[0].As<PaintingAnnotation>()
            .Body.As<Video>();
        thirdAnnotationBody.Should().NotBeNull("there is one non-choice video in NQ manifest");
    }
}
