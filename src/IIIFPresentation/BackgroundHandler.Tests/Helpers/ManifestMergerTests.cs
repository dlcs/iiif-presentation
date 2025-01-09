using BackgroundHandler.Helpers;
using FluentAssertions;
using IIIF;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
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
        var generatedManifest = GeneratedManifest();
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, generatedManifest);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(1);
        mergedManifest.Items[0].Width.Should().Be(100);
        mergedManifest.Items[0].Height.Should().Be(100);
        mergedManifest.Thumbnail.Count.Should().Be(1);
        mergedManifest.Metadata.Should().BeNull();
        mergedManifest.Label.Should().BeNull();
        mergedManifest.Items[0].Metadata.Should().NotBeNull();
    }
    
    [Fact]
    public void Merge_MergesFullManifestWithGeneratedManifest()
    {
        // Arrange
        var blankManifest = GeneratedManifest();
        blankManifest.Items[0].Width = 200;
        blankManifest.Items[0].Height = 200;
        var generatedManifest = GeneratedManifest();
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, generatedManifest);
        
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
        var blankManifest = GeneratedManifest();
        blankManifest.Items[0].Width = 200;
        blankManifest.Items[0].Height = 200;
        blankManifest.Thumbnail.Add(GenerateImage());
        blankManifest.Thumbnail[0].Service[0].Id = "generatedId";
        var generatedManifest = GeneratedManifest();
        
        // Act
        var mergedManifest = ManifestMerger.Merge(blankManifest, generatedManifest);
        
        // Assert
        mergedManifest.Items.Count.Should().Be(1);
        mergedManifest.Thumbnail[0].Service[0].Id.Should().Be("generatedId");
        mergedManifest.Thumbnail.Count.Should().Be(2);
        mergedManifest.Items[0].Width.Should().Be(100);
        mergedManifest.Items[0].Height.Should().Be(100);
    }

    private Manifest GeneratedManifest()
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
                new Canvas
                {
                    Id = nameof(Merge_MergesBlankManifestWithGeneratedManifest),
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
                }
            ],
            Metadata = GenerateMetadata()
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
