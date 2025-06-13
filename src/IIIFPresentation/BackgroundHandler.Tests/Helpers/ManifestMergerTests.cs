using BackgroundHandler.Helpers;
using FluentAssertions;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Models.DLCS;
using Canvas = IIIF.Presentation.V3.Canvas;
using Manifest = IIIF.Presentation.V3.Manifest;

namespace BackgroundHandler.Tests.Helpers;

public class ManifestMergerTests
{
    /*[Fact]
    public void Merge_MergesBlankManifestWithGeneratedManifest()
    {
        // Arrange
        var blankManifest = new Manifest();
        var assetId = $"1/2/{nameof(Merge_MergesBlankManifestWithGeneratedManifest)}";

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetId, c => c.WithImage())
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
    public void Merge_MergesBlankManifestWithGeneratedManifestWithCanvasLabel()
    {
        // Arrange
        var blankManifest = new Manifest();
        var assetId = $"1/2/{nameof(Merge_MergesBlankManifestWithGeneratedManifestWithCanvasLabel)}";

        var namedQueryManifest = ManifestTestCreator.New().WithCanvas(assetId, c => c.WithImage()).Build();
        namedQueryManifest.Items[0].Label = null;
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings([assetId]);
        canvasPaintings[0].CanvasLabel =
            new LanguageMap("canvasPaintingCanvasLabel", "generated canvas painting label");

        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, canvasPaintings, itemDictionary);

        // Assert
        mergedManifest.Items.Should().HaveCount(1);
        mergedManifest.Items[0].Width.Should().Be(110);
        mergedManifest.Items[0].Height.Should().Be(110);
        mergedManifest.Thumbnail.Should().BeNull();
        mergedManifest.Metadata.Should().BeNull();
        mergedManifest.Items[0].Label.Keys.Should().Contain("canvasPaintingCanvasLabel");
        mergedManifest.Items[0].Metadata.Should().BeNull();
    }

    [Fact]
    public void Merge_MergesFullManifestWithGeneratedManifest()
    {
        // Arrange
        var assetId = $"1/2/{nameof(Merge_MergesFullManifestWithGeneratedManifest)}";
        var blankManifest = ManifestTestCreator.New().WithCanvas(assetId, c => c.WithImage()).Build();
        blankManifest.Items[0].Width = 200;
        blankManifest.Items[0].Height = 200;
        var namedQueryManifest = ManifestTestCreator.New(assetId).WithCanvas(c => c.WithImage()).Build();
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);

        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, ManifestTestCreator.GenerateCanvasPaintings([assetId]),
            itemDictionary);

        // Assert
        mergedManifest.Items.Should().HaveCount(1);
        mergedManifest.Items[0].Width.Should().Be(110);
        mergedManifest.Items[0].Height.Should().Be(110);
    }

    [Fact]
    public void Merge_ShouldNotUpdateAttachedManifestThumbnail()
    {
        // Arrange
        var assetId = $"1/2/{nameof(Merge_ShouldNotUpdateAttachedManifestThumbnail)}";
        var secondThumbId = "sample_id";
        var baseManifest = ManifestTestCreator.New().WithCanvas(assetId, c => c.WithImage()).Build();
        baseManifest.Thumbnail.Add(ManifestTestCreator.GenerateImageService(secondThumbId));

        var namedQueryManifest = ManifestTestCreator.New().WithCanvas(assetId, c => c.WithImage()).Build();
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);

        // Act
        var mergedManifest = ManifestMerger.Merge(baseManifest, ManifestTestCreator.GenerateCanvasPaintings([assetId]),
            itemDictionary);

        // Assert
        mergedManifest.Items.Should().HaveCount(1);
        mergedManifest.Thumbnail.Count.Should().Be(2);
        mergedManifest.Thumbnail[0].Service[0].Id.Should()
            .Be(assetId, "GenerateManifest() adds service with provided assetId");
        mergedManifest.Thumbnail[1].Service[0].Id.Should().Be(secondThumbId);
    }

    [Fact]
    public void Merge_CorrectlyOrdersMultipleItems()
    {
        // Arrange
        var blankManifest = new Manifest();
        var assetIdOne = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1";
        var assetIdTwo = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2";

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(assetIdOne, c => c.WithImage())
            .WithCanvas(assetIdTwo, c => c.WithImage())
            .Build();
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings([assetIdOne, assetIdTwo]);
        canvasPaintings[0].CanvasOrder = 1;
        canvasPaintings[1].CanvasOrder = 0;

        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, canvasPaintings, itemDictionary);

        // Assert
        mergedManifest.Items.Should().HaveCount(2);
        mergedManifest.Thumbnail.Should().BeNull();
        mergedManifest.Items[0].Id.Should().Be(assetIdTwo, "order flipped due to canvas order");
        mergedManifest.Items[1].Id.Should().Be(assetIdOne, "order flipped due to canvas order");
    }

    [Fact]
    public void Merge_CorrectlyOrdersItemsWithChoiceOrderWithThumbnail()
    {
        // Arrange
        var blankManifest = new Manifest();

        var canvas0Choice0 = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1";
        var canvas1Choice2 = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2";
        var canvas0Choice1 = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_3";
        var canvas1Choice0 = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_4";
        var canvas2NoChoice = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_5";

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(canvas0Choice0, c => c.WithImage())
            .WithCanvas(canvas1Choice2, c => c.WithImage())
            .WithCanvas(canvas0Choice1, c => c.WithImage())
            .WithCanvas(canvas1Choice0, c => c.WithImage())
            .WithCanvas(canvas2NoChoice, c => c.WithImage())
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

        namedQueryManifest.Items[0].Thumbnail = null;
        namedQueryManifest.Items[1].Thumbnail = null;
        namedQueryManifest.Items[2].Thumbnail = null;
        namedQueryManifest.Items[3].Thumbnail = null;
        namedQueryManifest.Items[4].Thumbnail = null;

        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, canvasPaintings, itemDictionary);

        // Assert
        mergedManifest.Items.Should().HaveCount(3);
        mergedManifest.Thumbnail.Should().BeNull();

        // should be 1 + 3 then 4 + 2 then 5
        mergedManifest.Items[0].Id.Should().Be(canvas0Choice0);
        mergedManifest.Items[0].Thumbnail.Should().BeNull();
        mergedManifest.Items[1].Id.Should().Be(canvas1Choice0);
        mergedManifest.Items[1].Thumbnail.Should().BeNull();
        mergedManifest.Items[2].Id.Should().Be(canvas2NoChoice);
        mergedManifest.Items[2].Thumbnail.Should().BeNull();
    }

    [Fact]
    public void Merge_CorrectlyOrdersItemsWithChoiceOrder()
    {
        // Arrange
        var blankManifest = new Manifest();

        var canvas0Choice0 = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1";
        var canvas1Choice2 = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2";
        var canvas0Choice1 = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_3";
        var canvas1Choice0 = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_4";
        var canvas2NoChoice = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_5";

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(canvas0Choice0, c => c.WithImage())
            .WithCanvas(canvas1Choice2, c => c.WithImage())
            .WithCanvas(canvas0Choice1, c => c.WithImage())
            .WithCanvas(canvas1Choice0, c => c.WithImage())
            .WithCanvas(canvas2NoChoice, c => c.WithImage())
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
            new LanguageMap("canvasPaintingCanvasLabel", "generated canvas painting label");

        // make sure canvas label isn't used and label isn't set for canvas1Choice0
        canvasPaintings[1].CanvasLabel =
            new LanguageMap("canvasPaintingCanvasLabel", "generated canvas painting label");
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
        secondAnnotationBody.Items[0].As<Image>().Id.Should().Be(canvas1Choice0);
        secondAnnotationBody.Items[1].As<Image>().Id.Should().Be(canvas1Choice2);
        secondAnnotationBody.Items[1].As<Image>().Label.Should()
            .BeNull("label cannot be carried over from named query");
    }

    [Fact]
    public void Merge_CorrectlyMergesChoiceOrder_WhenUpdatingChoiceOrder()
    {
        // Arrange
        var existingItemBecomingCanvas0Choice0 =
            $"1/2/{nameof(Merge_CorrectlyMergesChoiceOrder_WhenUpdatingChoiceOrder)}_1";
        var canvas0Choice1 = $"1/2/{nameof(Merge_CorrectlyMergesChoiceOrder_WhenUpdatingChoiceOrder)}_2";
        var canvas1NoChoice = $"1/2/{nameof(Merge_CorrectlyMergesChoiceOrder_WhenUpdatingChoiceOrder)}_3";

        var minimalManifest = ManifestTestCreator.New()
            .WithCanvas(existingItemBecomingCanvas0Choice0, c => c.WithImage().ForceChoiceUse())
            .Build();

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(existingItemBecomingCanvas0Choice0, c => c.WithImage())
            .WithCanvas(canvas0Choice1, c => c.WithImage())
            .WithCanvas(canvas1NoChoice, c => c.WithImage())
            .Build();

        namedQueryManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Body.As<Image>().Label =
            new LanguageMap("after_update", "making sure label is filled out");

        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings([
            existingItemBecomingCanvas0Choice0,
            canvas0Choice1,
            canvas1NoChoice
        ]);

        canvasPaintings[0].CanvasOrder = 0;
        canvasPaintings[1].CanvasOrder = 0;
        canvasPaintings[2].CanvasOrder = 1;
        canvasPaintings[0].ChoiceOrder = 0;
        canvasPaintings[1].ChoiceOrder = 1;

        canvasPaintings[0].Label = null;
        minimalManifest.Items[0].Thumbnail = null;

        // Act
        var mergedManifest = ManifestMerger.Merge(minimalManifest, canvasPaintings, itemDictionary);

        // Assert
        mergedManifest.Items.Should().HaveCount(2);
        mergedManifest.Thumbnail.Should().ContainSingle(t => t.Service[0].Id == existingItemBecomingCanvas0Choice0);

        mergedManifest.Items[0].Id.Should().Be(existingItemBecomingCanvas0Choice0);
        mergedManifest.Items[1].Id.Should().Be(canvas1NoChoice);
        mergedManifest.Items[0].Thumbnail[0].Id.Should().Be($"{existingItemBecomingCanvas0Choice0}_CanvasThumbnail");

        var currentCanvasAnnotation = mergedManifest.GetCurrentCanvasAnnotationPage(0);

        currentCanvasAnnotation.Id.Should()
            .Be($"{existingItemBecomingCanvas0Choice0}_AnnotationPage");
        currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Id.Should()
            .Be($"{existingItemBecomingCanvas0Choice0}_PaintingAnnotation");

        var target = currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Target as Canvas;
        target.Id.Should().Be(existingItemBecomingCanvas0Choice0);

        var firstAnnotationBody = currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Body.As<PaintingChoice>();
        firstAnnotationBody.Items[0].As<Image>().Id.Should().Be(existingItemBecomingCanvas0Choice0);
        firstAnnotationBody.Items[0].As<Image>().Label.Should().BeNull("label cannot be carried over from named query");
        firstAnnotationBody.Items[1].As<Image>().Id.Should().Be(canvas0Choice1);
    }

    [Fact]
    public void Merge_CorrectlyMergesImageIntoChoiceOrder_WhenUpdatingChoiceOrder()
    {
        // Arrange
        var existingCanvas0Choice0 = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1";
        var canvas0Choice1 = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2";
        var canvas1NoChoice = $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_3";

        var existingManifest = ManifestTestCreator.New().WithCanvas(existingCanvas0Choice0, c => c.WithImage())
            .Build();

        var namedQueryManifest = ManifestTestCreator.New()
            .WithCanvas(existingCanvas0Choice0, c => c.WithImage())
            .WithCanvas(canvas0Choice1, c => c.WithImage())
            .WithCanvas(canvas1NoChoice, c => c.WithImage())
            .Build();

        namedQueryManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Body.As<Image>().Label =
            new LanguageMap("after_update", "making sure label is filled out");

        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);

        var canvasPaintings = ManifestTestCreator.GenerateCanvasPaintings([
            existingCanvas0Choice0,
            canvas0Choice1,
            canvas1NoChoice
        ]);

        canvasPaintings[0].CanvasOrder = 0;
        canvasPaintings[1].CanvasOrder = 0;
        canvasPaintings[2].CanvasOrder = 1;
        canvasPaintings[0].ChoiceOrder = 0;
        canvasPaintings[1].ChoiceOrder = 1;

        canvasPaintings[0].Label = null;
        canvasPaintings[0].Thumbnail = null;

        // Act
        var mergedManifest = ManifestMerger.Merge(existingManifest, canvasPaintings, itemDictionary);

        // Assert
        mergedManifest.Items.Should().HaveCount(2);
        mergedManifest.Items[0].Id.Should().Be(existingCanvas0Choice0);
        mergedManifest.Items[0].Thumbnail[0].Id.Should().Be($"{existingCanvas0Choice0}_CanvasThumbnail");
        mergedManifest.Items[1].Id.Should().Be(canvas1NoChoice);

        var currentCanvasAnnotation = mergedManifest.GetCurrentCanvasAnnotationPage(0);

        currentCanvasAnnotation.Id.Should().Be($"{existingCanvas0Choice0}_AnnotationPage");
        currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Id.Should()
            .Be($"{existingCanvas0Choice0}_PaintingAnnotation");

        var target = currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Target as Canvas;
        target.Id.Should().Be(existingCanvas0Choice0);

        var firstAnnotationBody = currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Body.As<PaintingChoice>();
        firstAnnotationBody.Items[0].As<Image>().Id.Should().Be(existingCanvas0Choice0);
        firstAnnotationBody.Items[0].As<Image>().Label.Should()
            .BeNull("label cannot be carried over from named query");
        firstAnnotationBody.Items[1].As<Image>().Id.Should().Be(canvas0Choice1);
    }*/
}
