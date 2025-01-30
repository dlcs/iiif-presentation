using BackgroundHandler.Helpers;
using FluentAssertions;
using IIIF;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Models.Database;
using Models.DLCS;
using Canvas = IIIF.Presentation.V3.Canvas;
using Manifest = IIIF.Presentation.V3.Manifest;

namespace BackgroundHandler.Tests.Helpers;

public class ManifestMergerTests
{
    [Fact]
    public void Merge_MergesBlankManifestWithGeneratedManifest()
    {
        // Arrange
        var blankManifest = new Manifest();
        var assetId = $"1/2/{nameof(Merge_MergesBlankManifestWithGeneratedManifest)}";
        
        var namedQueryManifest = GenerateManifest(assetId);
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest,
            GenerateCanvasPaintings([assetId]), itemDictionary,
            namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(1);
        mergedManifest.Items[0].Width.Should().Be(110);
        mergedManifest.Items[0].Height.Should().Be(110);
        mergedManifest.Thumbnail.Count.Should().Be(1);
        mergedManifest.Metadata.Should().BeNull();
        mergedManifest.Items[0].Label.Keys.Should().Contain("canvasPaintingLabel");;
        mergedManifest.Items[0].Metadata.Should().BeNull();
    }
    
    [Fact]
    public void Merge_MergesBlankManifestWithGeneratedManifestWithCanvasLabel()
    {
        // Arrange
        var blankManifest = new Manifest();
        var assetId = $"1/2/{nameof(Merge_MergesBlankManifestWithGeneratedManifestWithCanvasLabel)}";
        
        var namedQueryManifest = GenerateManifest(assetId);
        namedQueryManifest.Items[0].Label = null;
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        var canvasPaintings = GenerateCanvasPaintings([assetId]);
        canvasPaintings[0].CanvasLabel =
            new LanguageMap("canvasPaintingCanvasLabel", "generated canvas painting label");
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest,
            canvasPaintings, itemDictionary,
            namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(1);
        mergedManifest.Items[0].Width.Should().Be(110);
        mergedManifest.Items[0].Height.Should().Be(110);
        mergedManifest.Thumbnail.Count.Should().Be(1);
        mergedManifest.Metadata.Should().BeNull();
        mergedManifest.Items[0].Label.Keys.Should().Contain("canvasPaintingCanvasLabel");;
        mergedManifest.Items[0].Metadata.Should().BeNull();
    }
    
    [Fact]
    public void Merge_MergesFullManifestWithGeneratedManifest()
    {
        // Arrange
        var assetId = $"1/2/{nameof(Merge_MergesFullManifestWithGeneratedManifest)}";
        var blankManifest = GenerateManifest(assetId);
        blankManifest.Items[0].Width = 200;
        blankManifest.Items[0].Height = 200;
        var namedQueryManifest = GenerateManifest(assetId);
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest,
            GenerateCanvasPaintings([assetId]), itemDictionary,
            namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(1);
        mergedManifest.Thumbnail.Count.Should().Be(1);
        mergedManifest.Items[0].Width.Should().Be(110);
        mergedManifest.Items[0].Height.Should().Be(110);
    }
    
    [Fact]
    public void Merge_ShouldNotUpdateAttachedManifestThumbnail()
    {
        // Arrange
        var assetId = $"1/2/{nameof(Merge_ShouldNotUpdateAttachedManifestThumbnail)}";
        var blankManifest = GenerateManifest(assetId);
        blankManifest.Items[0].Width = 200;
        blankManifest.Items[0].Height = 200;
        blankManifest.Thumbnail.Add(GenerateImageService(assetId));
        blankManifest.Thumbnail[0].Service[0].Id = "namedQueryId";
        var namedQueryManifest = GenerateManifest(assetId);
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest,
            GenerateCanvasPaintings([assetId]), itemDictionary,
            namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(1);
        mergedManifest.Thumbnail[0].Service[0].Id.Should().Be("namedQueryId");
        mergedManifest.Thumbnail.Count.Should().Be(2);
        mergedManifest.Items[0].Width.Should().Be(110);
        mergedManifest.Items[0].Height.Should().Be(110);
    }
    
    [Fact]
    public void Merge_CorrectlyOrdersMultipleItems()
    {
        // Arrange
        var blankManifest = new Manifest();
        var namedQueryManifest = GenerateManifest($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        namedQueryManifest.Items.Add(GenerateCanvas($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2"));
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        var canvasPaintings = GenerateCanvasPaintings([
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1",
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2"
        ]);

        canvasPaintings[0].CanvasOrder = 1;
        canvasPaintings[1].CanvasOrder = 0;
        
        // Act
        var mergedManifest =
            ManifestMerger.Merge(blankManifest, canvasPaintings, itemDictionary, namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(2);
        mergedManifest.Thumbnail[0].Service[0].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        mergedManifest.Thumbnail.Count.Should().Be(1);
        // order flipped due to canvas order
        mergedManifest.Items[0].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2");
        mergedManifest.Items[1].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
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
        
        var namedQueryManifest = GenerateManifest(canvas0Choice0);
        namedQueryManifest.Items.Add(GenerateCanvas(canvas1Choice2));
        namedQueryManifest.Items.Add(GenerateCanvas(canvas0Choice1));
        namedQueryManifest.Items.Add(GenerateCanvas(canvas1Choice0));
        namedQueryManifest.Items.Add(GenerateCanvas(canvas2NoChoice));
        
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        var canvasPaintings = GenerateCanvasPaintings([
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
        var mergedManifest =
            ManifestMerger.Merge(blankManifest, canvasPaintings, itemDictionary, namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(3);
        mergedManifest.Thumbnail[0].Service[0].Id.Should().Be(canvas0Choice0);
        mergedManifest.Thumbnail.Count.Should().Be(1);
        // should be 1 + 3 then 4 + 2 then 5
        mergedManifest.Items[0].Id.Should().Be(canvas0Choice0);
        mergedManifest.Items[0].Label.Keys.Should().Contain("canvasPaintingCanvasLabel");
        mergedManifest.Items[1].Id.Should().Be(canvas1Choice0);
        mergedManifest.Items[1].Label.Keys.Should().Contain("canvasPaintingLabel");
        mergedManifest.Items[1].Label.Keys.Should().NotContain("canvasPaintingCanvasLabel");
        mergedManifest.Items[2].Id.Should().Be(canvas2NoChoice);
        mergedManifest.Items[2].Label.Should().BeNull("label cannot be carried over from the named query");
        
        var currentCanvasAnnotation = mergedManifest.GetCurrentCanvasAnnotationPage(0);
        
        currentCanvasAnnotation.Id.Should().Be($"{canvas0Choice0}_AnnotationPage");
        currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Id.Should().Be($"{canvas0Choice0}_PaintingAnnotation");

        var target = currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Target as Canvas;
        target.Id.Should().Be(canvas0Choice0);

        var firstAnnotationBody = currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Body.As<PaintingChoice>();
        firstAnnotationBody.Items[0].As<Image>().Id.Should().Be(canvas0Choice0);
        firstAnnotationBody.Items[1].As<Image>().Id.Should().Be(canvas0Choice1);
        firstAnnotationBody.Items[0].As<Image>().Label.Keys.Should().Contain("canvasPaintingLabel");
        
        var secondAnnotationBody =  mergedManifest.GetCurrentCanvasAnnotationPage(1).Items[0].As<PaintingAnnotation>().Body.As<PaintingChoice>();
        secondAnnotationBody.Items[0].As<Image>().Id.Should().Be(canvas1Choice0);
        secondAnnotationBody.Items[1].As<Image>().Id.Should().Be(canvas1Choice2);
        secondAnnotationBody.Items[1].As<Image>().Label.Should()
            .BeNull("label cannot be carried over from named query");
    }
    
    [Fact]
    public void Merge_CorrectlyMergesChoiceOrder_WhenUpdatingChoiceOrder()
    {
        // Arrange
        var existingItemBecomingCanvas0Choice0 = $"1/2/{nameof(Merge_CorrectlyMergesChoiceOrder_WhenUpdatingChoiceOrder)}_1";
        var canvas0Choice1 = $"1/2/{nameof(Merge_CorrectlyMergesChoiceOrder_WhenUpdatingChoiceOrder)}_2";
        var canvas1NoChoice = $"1/2/{nameof(Merge_CorrectlyMergesChoiceOrder_WhenUpdatingChoiceOrder)}_3";
        
        var minimalManifest = GenerateManifest(existingItemBecomingCanvas0Choice0, true);
        
        var namedQueryManifest = GenerateManifest(existingItemBecomingCanvas0Choice0);
        namedQueryManifest.Items.Add(GenerateCanvas(canvas0Choice1));
        namedQueryManifest.Items.Add(GenerateCanvas(canvas1NoChoice));
        
        namedQueryManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Body.As<Image>().Label =
            new LanguageMap("after_update", "making sure label is filled out");
        
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        var canvasPaintings = GenerateCanvasPaintings([
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
        
        // Act
        var mergedManifest =
            ManifestMerger.Merge(minimalManifest, canvasPaintings, itemDictionary, namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(2);
        mergedManifest.Thumbnail[0].Service[0].Id.Should()
            .Be(existingItemBecomingCanvas0Choice0);
        mergedManifest.Thumbnail.Count.Should().Be(1);

        mergedManifest.Items[0].Id.Should().Be(existingItemBecomingCanvas0Choice0);
        mergedManifest.Items[1].Id.Should().Be(canvas1NoChoice);
        
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
        
        var existingManifest = GenerateManifest(existingCanvas0Choice0);
        
        var namedQueryManifest = GenerateManifest(existingCanvas0Choice0);
        namedQueryManifest.Items.Add(GenerateCanvas(canvas0Choice1));
        namedQueryManifest.Items.Add(GenerateCanvas(canvas1NoChoice));
        
        namedQueryManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Body.As<Image>().Label =
            new LanguageMap("after_update", "making sure label is filled out");
        
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        var canvasPaintings = GenerateCanvasPaintings([
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
        
        // Act
        var mergedManifest =
            ManifestMerger.Merge(existingManifest, canvasPaintings, itemDictionary, namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(2);
        mergedManifest.Thumbnail[0].Service[0].Id.Should().Be(existingCanvas0Choice0);
        mergedManifest.Thumbnail.Count.Should().Be(1);
        mergedManifest.Items[0].Id.Should().Be(existingCanvas0Choice0);
        mergedManifest.Items[1].Id.Should().Be(canvas1NoChoice);

        var currentCanvasAnnotation = mergedManifest.GetCurrentCanvasAnnotationPage(0);

        currentCanvasAnnotation.Id.Should().Be($"{existingCanvas0Choice0}_AnnotationPage");
        currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Id.Should().Be($"{existingCanvas0Choice0}_PaintingAnnotation");
        
        var target = currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Target as Canvas;
        target.Id.Should().Be(existingCanvas0Choice0);
        
        var firstAnnotationBody = currentCanvasAnnotation.Items[0].As<PaintingAnnotation>().Body.As<PaintingChoice>();
        firstAnnotationBody.Items[0].As<Image>().Id.Should().Be(existingCanvas0Choice0);
        firstAnnotationBody.Items[0].As<Image>().Label.Should()
            .BeNull("label cannot be carried over from named query");
        firstAnnotationBody.Items[1].As<Image>().Id.Should().Be(canvas0Choice1);
    }

    private List<CanvasPainting> GenerateCanvasPaintings(List<string> idList)
    {
        var canvasOrder = 0;
        return idList.Select(id => new CanvasPainting
        {
            Id = id, AssetId = AssetId.FromString(id), CanvasOrder = canvasOrder++,
            Label = new LanguageMap("canvasPaintingLabel", "generated canvas painting label")
        }).ToList();
    }

    private Manifest GenerateManifest(string id, bool paintingChoice = false)
    {
        return new Manifest
        {
            Thumbnail =
            [
                GenerateImageService(id)
            ],
            Label = new LanguageMap("en", "someLabel"),
            Items =
            [
                GenerateCanvas(id, paintingChoice)
            ],
            Metadata = GenerateMetadata()
        };
    }

    private static Canvas GenerateCanvas(string id, bool paintingChoice = false)
    {
        return new Canvas
        {
            Id = $"{id}",
            Label = new LanguageMap("en", $"{id}"),
            Width = 110,
            Height = 110,
            Metadata = GenerateMetadata(),
            Items =
            [
                new AnnotationPage
                {
                    Id = $"{id}_AnnotationPage",
                    Label = new LanguageMap("en", $"{id}_AnnotationPage"),
                    Items =
                    [
                        new PaintingAnnotation
                        {
                            Id = $"{id}_PaintingAnnotation",
                            Label = new LanguageMap("en", $"PaintingAnnotation_{id}_PaintingAnnotation"),
                            Body = paintingChoice ? GeneratePaintingChoice(id) : GenerateImage(id),
                            Service = new List<IService>
                            {
                                new ImageService3
                                {
                                    Id = $"{id}_ImageService3",
                                    Label = new LanguageMap("en", $"{id}_ImageService3"),
                                    Profile = "level2"
                                }
                            }
                        }
                    ]
                }
            ]
        };
    }

    private static PaintingChoice GeneratePaintingChoice(string id)
    {
        return new PaintingChoice
        {
            Items = [GenerateImage(id)]
        };
    }

    private static Image GenerateImage(string id)
    {
        return new Image
        {
            Id = id,
            Width = 100,
            Height = 100,
        };
    }

    private static List<LabelValuePair> GenerateMetadata()
    {
        return [new(new LanguageMap("en", "label1"), new LanguageMap("en", "value1"))];
    }

    private static Image GenerateImageService(string id)
    {
        return new Image
        {
            Service =
            [
                new ImageService3
                {
                    Id = id,
                    Sizes = [new(100, 100)]
                }
            ],
            Label = new LanguageMap("en", $"{id}_Image")
        };
    }
}
