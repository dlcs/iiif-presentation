using BackgroundHandler.Helpers;
using FluentAssertions;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Models.DLCS;

namespace BackgroundHandler.Tests.Helpers;

public class ManifestMergerMixedContentTests
{
    [Fact]
    public void Merge_MergesBlankManifestWithGeneratedManifest()
    {
        // Arrange
        var blankManifest = new Manifest();
        var assetId = $"1/2/{nameof(Merge_MergesBlankManifestWithGeneratedManifest)}";

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithVideo())
            .Build();

        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);

        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, ManifestTestCreator.GenerateCanvasPaintings([assetId]),
            itemDictionary);

        // Assert
        mergedManifest.Items.Should().HaveCount(1);
        mergedManifest.Items[0].Width.Should().Be(110);
        mergedManifest.Items[0].Height.Should().Be(110);
        mergedManifest.Thumbnail.Should().BeNull("Thumbnail not defaulted with value from NQ");
        mergedManifest.Metadata.Should().BeNull();
        mergedManifest.Items[0].Label.Keys.Should().Contain("canvasPaintingLabel");
        mergedManifest.Items[0].Metadata.Should().BeNull();
    }

    [Fact]
    public void Merge_MixedContent_CorrectlyOrdersItemsWithChoiceOrder()
    {
        // Arrange
        var blankManifest = new Manifest();

        var canvas0Choice0 = $"1/2/{nameof(Merge_MixedContent_CorrectlyOrdersItemsWithChoiceOrder)}_1";
        var canvas1Choice2 = $"1/2/{nameof(Merge_MixedContent_CorrectlyOrdersItemsWithChoiceOrder)}_2";
        var canvas0Choice1 = $"1/2/{nameof(Merge_MixedContent_CorrectlyOrdersItemsWithChoiceOrder)}_3";
        var canvas1Choice0 = $"1/2/{nameof(Merge_MixedContent_CorrectlyOrdersItemsWithChoiceOrder)}_4";
        var canvas2NoChoice = $"1/2/{nameof(Merge_MixedContent_CorrectlyOrdersItemsWithChoiceOrder)}_5";

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(canvas0Choice0, c => c.WithImage())
            .WithCanvas(canvas1Choice2, c => c.WithSound())
            .WithCanvas(canvas0Choice1, c => c.WithImage())
            .WithCanvas(canvas1Choice0, c => c.WithSound())
            .WithCanvas(canvas2NoChoice, c => c.WithVideo())
            .Build();

        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings([
            canvas0Choice0,
            canvas1Choice2,
            canvas0Choice1,
            canvas1Choice0,
            canvas2NoChoice
        ]);

        canvasPaintings[0].CanvasOrder = 0;
        canvasPaintings[1].CanvasOrder = 1;
        canvasPaintings[2].CanvasOrder = 0;
        canvasPaintings[3].CanvasOrder = 1;
        canvasPaintings[4].CanvasOrder = 2;

        canvasPaintings[0].ChoiceOrder = 0;
        canvasPaintings[1].ChoiceOrder = 1;
        canvasPaintings[2].ChoiceOrder = 1;
        canvasPaintings[3].ChoiceOrder = 0;
        canvasPaintings[4].ChoiceOrder = 0;

        canvasPaintings[0].CanvasLabel =
            new("canvasPaintingCanvasLabel", "generated canvas painting label");

        // make sure canvas label isn't used and label isn't set for canvas1Choice0
        canvasPaintings[1].CanvasLabel =
            new("canvasPaintingCanvasLabel", "generated canvas painting label");
        canvasPaintings[1].Label = null;

        // remove labels from canvas2NoChoice
        canvasPaintings[4].CanvasLabel = null;
        canvasPaintings[4].Label = null;

        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, canvasPaintings, itemDictionary);

        // Assert
        mergedManifest.Items.Should().HaveCount(3);
        mergedManifest.Thumbnail.Should().BeNull();

        // should be 1 + 3 then 4 + 2 then 5
        mergedManifest.Items[0].Id.Should().Be(canvas0Choice0);
        mergedManifest.Items[0].Label.Keys.Should().Contain("canvasPaintingCanvasLabel");
        mergedManifest.Items[0].Thumbnail[0].Id.Should().Be($"{canvas0Choice0}_CanvasThumbnail");
        mergedManifest.Items[1].Id.Should().Be(canvas1Choice0);
        mergedManifest.Items[1].Label.Keys.Should().Contain("canvasPaintingLabel");
        mergedManifest.Items[1].Label.Keys.Should().NotContain("canvasPaintingCanvasLabel");
        mergedManifest.Items[1].Thumbnail[0].Id.Should().Be($"{canvas1Choice0}_CanvasThumbnail");
        mergedManifest.Items[2].Id.Should().Be(canvas2NoChoice);
        mergedManifest.Items[2].Label.Should().BeNull("label cannot be carried over from the named query");
        mergedManifest.Items[2].Thumbnail[0].Id.Should().Be($"{canvas2NoChoice}_CanvasThumbnail");

        var currentCanvasAnnotation = mergedManifest.GetCurrentCanvasAnnotationPage(0);

        currentCanvasAnnotation.Id.Should().Be($"{canvas0Choice0}_AnnotationPage");
        currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Id.Should()
            .Be($"{canvas0Choice0}_PaintingAnnotation");

        var target = currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Target as Canvas;
        target.Id.Should().Be(canvas0Choice0);

        var firstAnnotationBody = currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Body.As<PaintingChoice>();
        firstAnnotationBody.Items[0].As<Image>().Id.Should().Be(canvas0Choice0);
        firstAnnotationBody.Items[1].As<Image>().Id.Should().Be(canvas0Choice1);
        firstAnnotationBody.Items[0].As<Image>().Label.Keys.Should().Contain("canvasPaintingLabel");

        var secondAnnotationBody = mergedManifest.GetCurrentCanvasAnnotationPage(1).Items[0].As<PaintingAnnotation>()
            .Body.As<PaintingChoice>();
        secondAnnotationBody.Items[0].As<Sound>().Id.Should().Be(canvas1Choice0);
        secondAnnotationBody.Items[1].As<Sound>().Id.Should().Be(canvas1Choice2);
        secondAnnotationBody.Items[1].As<Sound>().Label.Should()
            .BeNull("label cannot be carried over from named query");

        var thirdAnnotationBody = mergedManifest.GetCurrentCanvasAnnotationPage(2).Items[0].As<PaintingAnnotation>()
            .Body.As<Video>();

        thirdAnnotationBody.Should().NotBeNull("there is one non-choice video in NQ manifest");
    }

    [Fact]
    public void Merge_MixedContent_NoChoicesOfChoices()
    {
        // Arrange
        var blankManifest = new Manifest();

        var canvas0Choice0 = $"1/2/{nameof(Merge_MixedContent_CorrectlyOrdersItemsWithChoiceOrder)}_1";
        var canvas1Choice2 = $"1/2/{nameof(Merge_MixedContent_CorrectlyOrdersItemsWithChoiceOrder)}_2";
        var canvas0Choice1 = $"1/2/{nameof(Merge_MixedContent_CorrectlyOrdersItemsWithChoiceOrder)}_3";
        var canvas1Choice0 = $"1/2/{nameof(Merge_MixedContent_CorrectlyOrdersItemsWithChoiceOrder)}_4";
        var canvas2NoChoice = $"1/2/{nameof(Merge_MixedContent_CorrectlyOrdersItemsWithChoiceOrder)}_5";

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(canvas0Choice0, c => c.WithImages(2))
            .WithCanvas(canvas1Choice2, c => c.WithSounds(2))
            .WithCanvas(canvas0Choice1, c => c.WithImages(2))
            .WithCanvas(canvas1Choice0, c => c.WithSounds(2))
            .WithCanvas(canvas2NoChoice, c => c.WithVideos(2))
            .Build();

        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings([
            canvas0Choice0,
            canvas1Choice2,
            canvas0Choice1,
            canvas1Choice0,
            canvas2NoChoice
        ]);

        canvasPaintings[0].CanvasOrder = 0;
        canvasPaintings[1].CanvasOrder = 1;
        canvasPaintings[2].CanvasOrder = 0;
        canvasPaintings[3].CanvasOrder = 1;
        canvasPaintings[4].CanvasOrder = 2;

        canvasPaintings[0].ChoiceOrder = 0;
        canvasPaintings[1].ChoiceOrder = 1;
        canvasPaintings[2].ChoiceOrder = 1;
        canvasPaintings[3].ChoiceOrder = 0;
        canvasPaintings[4].ChoiceOrder = 0;

        canvasPaintings[0].CanvasLabel =
            new("canvasPaintingCanvasLabel", "generated canvas painting label");

        // make sure canvas label isn't used and label isn't set for canvas1Choice0
        canvasPaintings[1].CanvasLabel =
            new("canvasPaintingCanvasLabel", "generated canvas painting label");
        canvasPaintings[1].Label = null;

        // remove labels from canvas2NoChoice
        canvasPaintings[4].CanvasLabel = null;
        canvasPaintings[4].Label = null;

        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, canvasPaintings, itemDictionary);

        // Assert
        mergedManifest.Items.Should().HaveCount(3);
        mergedManifest.Thumbnail.Should().BeNull();

        // should be 1 + 3 then 4 + 2 then 5
        mergedManifest.Items[0].Id.Should().Be(canvas0Choice0);
        mergedManifest.Items[0].Label.Keys.Should().Contain("canvasPaintingCanvasLabel");
        mergedManifest.Items[0].Thumbnail[0].Id.Should().Be($"{canvas0Choice0}_CanvasThumbnail");
        mergedManifest.Items[1].Id.Should().Be(canvas1Choice0);
        mergedManifest.Items[1].Label.Keys.Should().Contain("canvasPaintingLabel");
        mergedManifest.Items[1].Label.Keys.Should().NotContain("canvasPaintingCanvasLabel");
        mergedManifest.Items[1].Thumbnail[0].Id.Should().Be($"{canvas1Choice0}_CanvasThumbnail");
        mergedManifest.Items[2].Id.Should().Be(canvas2NoChoice);
        mergedManifest.Items[2].Label.Should().BeNull("label cannot be carried over from the named query");
        mergedManifest.Items[2].Thumbnail[0].Id.Should().Be($"{canvas2NoChoice}_CanvasThumbnail");

        var currentCanvasAnnotation = mergedManifest.GetCurrentCanvasAnnotationPage(0);

        currentCanvasAnnotation.Id.Should().Be($"{canvas0Choice0}_AnnotationPage");
        currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Id.Should()
            .Be($"{canvas0Choice0}_PaintingAnnotation");

        var target = currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Target as Canvas;
        target.Id.Should().Be(canvas0Choice0);

        var firstAnnotationBody = currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Body.As<PaintingChoice>();
        firstAnnotationBody.Items.OfType<Image>().Should().HaveCount(4, "2+2 images merged");

        var secondAnnotationBody = mergedManifest.GetCurrentCanvasAnnotationPage(1).Items[0].As<PaintingAnnotation>()
            .Body.As<PaintingChoice>();
        secondAnnotationBody.Items.OfType<Sound>().Should().HaveCount(4, "2+2 sounds merged");

        var thirdAnnotationBody = mergedManifest.GetCurrentCanvasAnnotationPage(2).Items[0].As<PaintingAnnotation>()
            .Body.As<PaintingChoice>();

        thirdAnnotationBody.Items.OfType<Video>().Should().HaveCount(2, "2 video choice in NQ");
    }
}
