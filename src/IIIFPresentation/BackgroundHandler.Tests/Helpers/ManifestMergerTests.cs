using BackgroundHandler.Helpers;
using FluentAssertions;
using IIIF;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Models.Database;
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
        var generatedManifest = GeneratedManifest(nameof(Merge_MergesBlankManifestWithGeneratedManifest));
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, generatedManifest,
            GenerateCanvasPaintings([nameof(Merge_MergesBlankManifestWithGeneratedManifest)]));
        
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
        var blankManifest = GeneratedManifest(nameof(Merge_MergesFullManifestWithGeneratedManifest));
        blankManifest.Items[0].Width = 200;
        blankManifest.Items[0].Height = 200;
        var generatedManifest = GeneratedManifest(nameof(Merge_MergesFullManifestWithGeneratedManifest));
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, generatedManifest,
            GenerateCanvasPaintings([nameof(Merge_MergesFullManifestWithGeneratedManifest)]));
        
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
        var blankManifest = GeneratedManifest(nameof(Merge_ShouldNotUpdateAttachedManifestThumbnail));
        blankManifest.Items[0].Width = 200;
        blankManifest.Items[0].Height = 200;
        blankManifest.Thumbnail.Add(GenerateImage());
        blankManifest.Thumbnail[0].Service[0].Id = "generatedId";
        var generatedManifest = GeneratedManifest(nameof(Merge_ShouldNotUpdateAttachedManifestThumbnail));
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, generatedManifest,
            GenerateCanvasPaintings([nameof(Merge_ShouldNotUpdateAttachedManifestThumbnail)]));
        
        // Assert
        mergedManifest.Items.Count.Should().Be(1);
        mergedManifest.Thumbnail[0].Service[0].Id.Should().Be("generatedId");
        mergedManifest.Thumbnail.Count.Should().Be(2);
        mergedManifest.Items[0].Width.Should().Be(100);
        mergedManifest.Items[0].Height.Should().Be(100);
    }
    
    [Fact]
    public void Merge_CorrectlyOrdersMultipleItems()
    {
        // Arrange
        var blankManifest = new Manifest();
        var generatedManifest = GeneratedManifest($"{nameof(Merge_ShouldNotUpdateAttachedManifestThumbnail)}_1");
        generatedManifest.Items.Add(GenerateCanvas($"{nameof(Merge_ShouldNotUpdateAttachedManifestThumbnail)}_2"));
        
        var canvasPaintings = GenerateCanvasPaintings([
            $"{nameof(Merge_ShouldNotUpdateAttachedManifestThumbnail)}_1",
            $"{nameof(Merge_ShouldNotUpdateAttachedManifestThumbnail)}_2"
        ]);

        canvasPaintings[0].CanvasOrder = 1;
        canvasPaintings[1].CanvasOrder = 0;
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, generatedManifest,
            canvasPaintings);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(2);
        mergedManifest.Thumbnail[0].Service[0].Id.Should().Be("imageId");
        mergedManifest.Thumbnail.Count.Should().Be(1);
        // order flipped due to canvas order
        mergedManifest.Items[0].Id.Should().Be($"{nameof(Merge_ShouldNotUpdateAttachedManifestThumbnail)}_2");
        mergedManifest.Items[1].Id.Should().Be($"{nameof(Merge_ShouldNotUpdateAttachedManifestThumbnail)}_1");
    }

    private List<CanvasPainting> GenerateCanvasPaintings(List<string> idList)
    {
        var canvasOrder = 0;
        return idList.Select(id => new CanvasPainting{Id = id, AssetId = id, CanvasOrder = canvasOrder++}).ToList();
    }

    private Manifest GeneratedManifest(string id)
    {
        return new Manifest
        {
            Thumbnail =
            [
                GenerateImage()
            ],
            Label = new LanguageMap("en", "someLabel"),
            Items =
            [
                GenerateCanvas(id)
            ],
            Metadata = GenerateMetadata()
        };
    }

    private static Canvas GenerateCanvas(string id)
    {
        return new Canvas
        {
            Id = id,
            Width = 100,
            Height = 100,
            Metadata = GenerateMetadata(),
            Annotations =
            [
                new AnnotationPage
                {
                    Items =
                    [
                        new PaintingAnnotation
                        {
                            Body = new Image
                            {
                                Width = 100,
                                Height = 100,
                            },
                            Service = new List<IService>
                            {
                                new ImageService3
                                {
                                    Profile = "level2"
                                }
                            }
                        }
                    ]
                }
            ]
        };
    }

    private static List<LabelValuePair> GenerateMetadata()
    {
        return new List<LabelValuePair>()
        {
            new(new LanguageMap("en", "label1"), new LanguageMap("en", "value1"))
        };
    }

    private static Image GenerateImage()
    {
        return new Image
        {
            Service =
            [
                new ImageService3
                {
                    Id = "imageId",
                    Sizes = [new(100, 100)]
                }
            ]
        };
    }
}
