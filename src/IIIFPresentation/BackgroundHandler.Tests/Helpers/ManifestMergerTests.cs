﻿using BackgroundHandler.Helpers;
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
        
        var namedQueryManifest = GeneratedManifest(assetId);
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest,
            GenerateCanvasPaintings([assetId]), itemDictionary,
            namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(1);
        mergedManifest.Items[0].Width.Should().Be(100);
        mergedManifest.Items[0].Height.Should().Be(100);
        mergedManifest.Thumbnail.Count.Should().Be(1);
        mergedManifest.Metadata.Should().BeNull();
        mergedManifest.Label.Should().BeNull();
        mergedManifest.Items[0].Metadata.Should().BeNull();
    }
    
    [Fact]
    public void Merge_MergesFullManifestWithGeneratedManifest()
    {
        // Arrange
        var assetId = $"1/2/{nameof(Merge_MergesFullManifestWithGeneratedManifest)}";
        var blankManifest = GeneratedManifest(assetId);
        blankManifest.Items[0].Width = 200;
        blankManifest.Items[0].Height = 200;
        var namedQueryManifest = GeneratedManifest(assetId);
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest,
            GenerateCanvasPaintings([assetId]), itemDictionary,
            namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(1);
        mergedManifest.Thumbnail.Count.Should().Be(1);
        mergedManifest.Items[0].Width.Should().Be(100);
        mergedManifest.Items[0].Height.Should().Be(100);
    }
    
    [Fact]
    public void Merge_ShouldNotUpdateAttachedManifestThumbnail()
    {
        // Arrange
        var assetId = $"1/2/{nameof(Merge_ShouldNotUpdateAttachedManifestThumbnail)}";
        var blankManifest = GeneratedManifest(assetId);
        blankManifest.Items[0].Width = 200;
        blankManifest.Items[0].Height = 200;
        blankManifest.Thumbnail.Add(GenerateImageService(assetId));
        blankManifest.Thumbnail[0].Service[0].Id = "namedQueryId";
        var namedQueryManifest = GeneratedManifest(assetId);
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest,
            GenerateCanvasPaintings([assetId]), itemDictionary,
            namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(1);
        mergedManifest.Thumbnail[0].Service[0].Id.Should().Be("namedQueryId");
        mergedManifest.Thumbnail.Count.Should().Be(2);
        mergedManifest.Items[0].Width.Should().Be(100);
        mergedManifest.Items[0].Height.Should().Be(100);
    }
    
    [Fact]
    public void Merge_CorrectlyOrdersMultipleItems()
    {
        // Arrange
        var blankManifest = new Manifest();
        var namedQueryManifest = GeneratedManifest($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
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
        var namedQueryManifest = GeneratedManifest($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        namedQueryManifest.Items.Add(GenerateCanvas($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2"));
        namedQueryManifest.Items.Add(GenerateCanvas($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_3"));
        namedQueryManifest.Items.Add(GenerateCanvas($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_4"));
        namedQueryManifest.Items.Add(GenerateCanvas($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_5"));
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        var canvasPaintings = GenerateCanvasPaintings([
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1",
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2",
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_3",
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_4",
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_5"
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
        
        // Act
        var mergedManifest =
            ManifestMerger.Merge(blankManifest, canvasPaintings, itemDictionary, namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(3);
        mergedManifest.Thumbnail[0].Service[0].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        mergedManifest.Thumbnail.Count.Should().Be(1);
        // should be 1 + 3 then 4 + 2 then 5
        mergedManifest.Items[0].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        mergedManifest.Items[1].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_4");
        mergedManifest.Items[2].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_5");
        mergedManifest.Items[0].Items[0].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1_AnnotationPage");
        mergedManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1_PaintingAnnotation");

        var firstAnnotationBody = mergedManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Body.As<PaintingChoice>();
        firstAnnotationBody.Items[0].As<Image>().Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        firstAnnotationBody.Items[1].As<Image>().Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_3");
        
        var secondAnnotationBody = mergedManifest.Items[1].Items[0].Items[0].As<PaintingAnnotation>().Body.As<PaintingChoice>();
        secondAnnotationBody.Items[0].As<Image>().Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_4");
        secondAnnotationBody.Items[1].As<Image>().Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2");
    }
    
    [Fact]
    public void Merge_CorrectlyMergesChoiceOrder_WhenUpdatingChoiceOrder()
    {
        // Arrange
        var blankManifest = GeneratedManifest($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1", true);

        blankManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Body.As<PaintingChoice>().Items[0].As<Image>().Label =
            new LanguageMap("before_update", "before update");
        
        var namedQueryManifest = GeneratedManifest($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        namedQueryManifest.Items.Add(GenerateCanvas($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2"));
        namedQueryManifest.Items.Add(GenerateCanvas($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_3"));
        
        namedQueryManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Body.As<Image>().Label =
            new LanguageMap("after_update", "after update");
        
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        var canvasPaintings = GenerateCanvasPaintings([
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1",
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2",
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_3"
        ]);

        canvasPaintings[0].CanvasOrder = 0;
        canvasPaintings[1].CanvasOrder = 0;
        canvasPaintings[2].CanvasOrder = 1;
        canvasPaintings[0].ChoiceOrder = 0;
        canvasPaintings[1].ChoiceOrder = 1;
        
        // Act
        var mergedManifest =
            ManifestMerger.Merge(blankManifest, canvasPaintings, itemDictionary, namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(2);
        mergedManifest.Thumbnail[0].Service[0].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        mergedManifest.Thumbnail.Count.Should().Be(1);
        // should be 1 + 3 then 4 + 2 then 5
        mergedManifest.Items[0].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        mergedManifest.Items[1].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_3");
        mergedManifest.Items[0].Items[0].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1_AnnotationPage");
        mergedManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1_PaintingAnnotation");

        var firstAnnotationBody = mergedManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Body.As<PaintingChoice>();
        firstAnnotationBody.Items[0].As<Image>().Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        firstAnnotationBody.Items[0].As<Image>().Label.Keys.Should().Contain("after_update");
        firstAnnotationBody.Items[1].As<Image>().Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2");
    }
    
    [Fact]
    public void Merge_CorrectlyMergesImageIntoChoiceOrder_WhenUpdatingChoiceOrder()
    {
        // Arrange
        var blankManifest = GeneratedManifest($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        
        var namedQueryManifest = GeneratedManifest($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        namedQueryManifest.Items.Add(GenerateCanvas($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2"));
        namedQueryManifest.Items.Add(GenerateCanvas($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_3"));
        
        namedQueryManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Body.As<Image>().Label =
            new LanguageMap("after_update", "after update");
        
        var itemDictionary = namedQueryManifest.Items.ToDictionary(i => AssetId.FromString(i.Id), i => i);
        
        var canvasPaintings = GenerateCanvasPaintings([
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1",
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2",
            $"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_3"
        ]);

        canvasPaintings[0].CanvasOrder = 0;
        canvasPaintings[1].CanvasOrder = 0;
        canvasPaintings[2].CanvasOrder = 1;
        canvasPaintings[0].ChoiceOrder = 0;
        canvasPaintings[1].ChoiceOrder = 1;
        
        // Act
        var mergedManifest =
            ManifestMerger.Merge(blankManifest, canvasPaintings, itemDictionary, namedQueryManifest.Thumbnail);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(2);
        mergedManifest.Thumbnail[0].Service[0].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        mergedManifest.Thumbnail.Count.Should().Be(1);
        // should be 1 + 3 then 4 + 2 then 5
        mergedManifest.Items[0].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        mergedManifest.Items[1].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_3");

        mergedManifest.Items[0].Items[0].Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1_AnnotationPage");
        mergedManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1_PaintingAnnotation");
        var firstAnnotationBody = mergedManifest.Items[0].Items[0].Items[0].As<PaintingAnnotation>().Body.As<PaintingChoice>();
        firstAnnotationBody.Items[0].As<Image>().Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_1");
        firstAnnotationBody.Items[0].As<Image>().Label.Keys.Should().Contain("after_update");
        firstAnnotationBody.Items[1].As<Image>().Id.Should().Be($"1/2/{nameof(Merge_CorrectlyOrdersMultipleItems)}_2");
    }

    private List<CanvasPainting> GenerateCanvasPaintings(List<string> idList)
    {
        var canvasOrder = 0;
        return idList.Select(id => new CanvasPainting{Id = id, AssetId = AssetId.FromString(id), CanvasOrder = canvasOrder++}).ToList();
    }

    private Manifest GeneratedManifest(string id, bool paintingChoice = false)
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
            Width = 100,
            Height = 100,
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
        return new PaintingChoice()
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
            ]
        };
    }
}
