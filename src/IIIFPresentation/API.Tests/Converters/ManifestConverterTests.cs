using API.Converters;
using Core.Infrastructure;
using Core.Web;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.Manifest;
using Models.Database.General;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using Repository.Paths;
using Test.Helpers.Helpers;
using CanvasPainting = Models.Database.CanvasPainting;
using DBManifest = Models.Database.Collections.Manifest;
using TestPathGenerator = API.Tests.Helpers.TestPathGenerator;

namespace API.Tests.Converters;

public class ManifestConverterTests
{
    private readonly IPathGenerator pathGenerator = TestPathGenerator.CreatePathGenerator("base", Uri.UriSchemeHttp);

    private readonly IPathRewriteParser pathRewriteParser =
        new PathRewriteParser(Options.Create(PathRewriteOptions.Default), new NullLogger<PathRewriteParser>());
    
    [Fact]
    public void SetGeneratedFields_AddsCustomContext()
    {
        // Arrange
        var iiifManifest = new PresentationManifest();
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            Id = "id",
            Hierarchy = [new Hierarchy { Slug = "slug" }],
        };

        var expectedContexts = new List<string>
        {
            "http://tbc.org/iiif-repository/1/context.json",
            "http://iiif.io/api/presentation/3/context.json"
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, pathGenerator);

        // Assert
        result.Context.As<List<string>>().Should().BeEquivalentTo(expectedContexts);
    }
    
    [Fact]
    public void SetGeneratedFields_SetsId()
    {
        // Arrange
        var iiifManifest = new PresentationManifest();
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            Id = "id",
            Hierarchy = [new Hierarchy { Slug = "slug" }],
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, pathGenerator);

        // Assert
        result.Id.Should().Be("http://base/123/manifests/id");
    }
    
    [Fact]
    public void SetGeneratedFields_SetsAuditFields()
    {
        // Arrange
        var iiifManifest = new PresentationManifest();
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow.AddDays(1),
            CreatedBy = "creator",
            ModifiedBy = "modifier",
            Id = "id",
            Hierarchy = [new Hierarchy { Slug = "slug" }],
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, pathGenerator);

        // Assert
        result.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        result.Modified.Should().BeCloseTo(DateTime.UtcNow.AddDays(1), TimeSpan.FromSeconds(2));
        
        result.CreatedBy.Should().Be("creator");
        result.ModifiedBy.Should().Be("modifier");
    }
    
    [Fact]
    public void SetGeneratedFields_SetsParentAndSlug_FromSingleHierarchyByDefault()
    {
        // Arrange
        var iiifManifest = new PresentationManifest
        {
            Parent = "parent-will-be-overriden",
            Slug = "slug-will-be-overriden",
        };
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow.AddDays(1),
            CreatedBy = "creator",
            ModifiedBy = "modifier",
            Id = "id",
            Hierarchy = [new Hierarchy { Slug = "hierarchy-slug", Parent = "hierarchy-parent" }],
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, pathGenerator);

        // Assert
        result.Slug.Should().Be("hierarchy-slug");
        result.Parent.Should().Be("http://base/0/collections/hierarchy-parent", "Always use FlatId");
    }
    
    [Fact]
    public void SetGeneratedFields_SetsParentAndSlug_FromHierarchyUsingFactory()
    {
        // Arrange
        var iiifManifest = new PresentationManifest
        {
            Parent = "parent-will-be-overriden",
            Slug = "slug-will-be-overriden",
        };
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow.AddDays(1),
            CreatedBy = "creator",
            ModifiedBy = "modifier",
            Id = "id",
            Hierarchy = [
                new Hierarchy { Slug = "hierarchy-slug", Parent = "hierarchy-parent" },
                new Hierarchy { Slug = "other-slug", Parent = "other-parent" },],
        };
        
        // Act
        var result =
            iiifManifest.SetGeneratedFields(dbManifest, pathGenerator, null, manifest => manifest.Hierarchy.Last());

        // Assert
        result.Slug.Should().Be("other-slug");
        result.Parent.Should().Be("http://base/0/collections/other-parent", "Always use FlatId");
    }
    
    [Fact]
    public void SetGeneratedFields_SetsCanvasPainting_IfPresent()
    {
        // Arrange
        var iiifManifest = new PresentationManifest();
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow.AddDays(1),
            CreatedBy = "creator",
            ModifiedBy = "modifier",
            Id = "id",
            Hierarchy = [new Hierarchy { Slug = "slug" }],
            CanvasPaintings =
            [
                new CanvasPainting
                {
                    CanvasOriginalId = new Uri("http://example.test/canvas1"),
                    CustomerId = 123,
                    Id = "the-canvas",
                    ChoiceOrder = 10,
                    CanvasOrder = 100,
                    AssetId = new AssetId(1, 2, "assetId")
                }
            ]
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, pathGenerator);

        // Assert
        var paintedResource = result.PaintedResources.Single();
        paintedResource.CanvasPainting.CanvasId.Should().Be("http://base/123/canvases/the-canvas");
        paintedResource.CanvasPainting.ChoiceOrder.Should().Be(10);
        paintedResource.CanvasPainting.CanvasOrder.Should().Be(100);
        paintedResource.CanvasPainting.CanvasOriginalId.Should().Be("http://example.test/canvas1");

        paintedResource.Asset.GetValue("@id").ToString().Should().Be("https://dlcs.test/customers/1/spaces/2/images/assetId");
    }
    
    [Fact]
    public void SetGeneratedFields_SetsCanvasPainting_WithoutAssetId()
    {
        // Arrange
        var iiifManifest = new PresentationManifest();
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow.AddDays(1),
            CreatedBy = "creator",
            ModifiedBy = "modifier",
            Id = "id",
            Hierarchy = [new Hierarchy { Slug = "slug" }],
            CanvasPaintings =
            [
                new CanvasPainting
                {
                    CanvasOriginalId = new Uri("http://example.test/canvas1"),
                    CustomerId = 123,
                    Id = "the-canvas",
                    ChoiceOrder = 10,
                    CanvasOrder = 100
                }
            ]
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, pathGenerator);

        // Assert
        var paintedResource = result.PaintedResources.Single();
        paintedResource.CanvasPainting.CanvasId.Should().Be("http://base/123/canvases/the-canvas");
        paintedResource.CanvasPainting.ChoiceOrder.Should().Be(10);
        paintedResource.CanvasPainting.CanvasOrder.Should().Be(100);
        paintedResource.CanvasPainting.CanvasOriginalId.Should().Be("http://example.test/canvas1");
        paintedResource.Asset.Should().BeNull();
    }
    
    [Fact]
    public void SetGeneratedFields_SetsSpace()
    {
        // Arrange
        var iiifManifest = new PresentationManifest();
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            SpaceId = 321,
            Id = "id",
            Hierarchy = [new Hierarchy { Slug = "slug" }],
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, pathGenerator);

        // Assert
        result.Space.Should().Be("https://dlcs.test/customers/123/spaces/321");
    }
    
    [Fact]
    public void GenerateProvisionalCanvases_SetsItems_IfNotSet()
    {
        var canvasPaintings = new List<CanvasPainting>
        {
            new() { AssetId = new AssetId(1, 2, "1-i"), CanvasOrder = 0, Id = "first" },
            new()
            {
                AssetId = new AssetId(1, 2, "1-ii-b"), CanvasOrder = 1, ChoiceOrder = 2, Id = "first",
                Target = "xywh=10,100,200,200"
            },
            new()
            {
                AssetId = new AssetId(1, 2, "1-ii-a"), CanvasOrder = 1, ChoiceOrder = 1, Id = "first",
                Target = "xywh=0,0,200,200"
            },
            new()
            {
                AssetId = new AssetId(1, 2, "1-iii"), CanvasOrder = 2, Id = "first", Target = "xywh=200,400,200,200"
            },
            new() { AssetId = new AssetId(1, 2, "2"), CanvasOrder = 3, Id = "alpha" },
        };

        var expectedItems = new List<Canvas>
        {
            new()
            {
                Id = "http://base/0/canvases/first",
                Items =
                [
                    new AnnotationPage
                    {
                        Id = "http://base/0/canvases/first/annopages/0",
                        Items =
                        [
                            new PaintingAnnotation
                            {
                                Id = "http://base/0/canvases/first/annotations/0",
                                Behavior = [Behavior.Processing],
                                Target = new Canvas { Id = "http://base/0/canvases/first" },
                                Body = null,
                            },
                            new PaintingAnnotation
                            {
                                Id = "http://base/0/canvases/first/annotations/1",
                                Behavior = [Behavior.Processing],
                                Target = new Canvas { Id = "http://base/0/canvases/first#xywh=0,0,200,200" },
                                Body = new PaintingChoice(),
                            },
                            new PaintingAnnotation
                            {
                                Id = "http://base/0/canvases/first/annotations/2",
                                Behavior = [Behavior.Processing],
                                Target = new Canvas { Id = "http://base/0/canvases/first#xywh=200,400,200,200" },
                                Body = null,
                            },
                        ]
                    }
                ]
            },
            new()
            {
                Id = "http://base/0/canvases/alpha",
                Items =
                [
                    new AnnotationPage
                    {
                        Id = "http://base/0/canvases/alpha/annopages/3",
                        Items =
                        [
                            new PaintingAnnotation
                            {
                                Id = "http://base/0/canvases/alpha/annotations/3",
                                Behavior = [Behavior.Processing],
                                Target = new Canvas { Id = "http://base/0/canvases/alpha" },
                                Body = null,
                            }
                        ]
                    }
                ]
            }
        };
        
        // Act
        var result = canvasPaintings.GenerateProvisionalCanvases(pathGenerator, [], pathRewriteParser);
        
        // Assert
        for (int i = 0; i < expectedItems.Count; i++)
        {
            // Comparing individually makes errors earlier to grok
            result[i].Should().BeEquivalentTo(expectedItems[i], opts => opts.RespectingRuntimeTypes(),
                $"Item {i} should be equivalent");
        }
    }

    [Fact]
    public void GenerateProvisionalCanvases_GeneratesItemsFromCanvas_IfSetWithCanvasList()
    {
        // Arrange
        var canvasList = new List<Canvas>
        {
            ManifestTestCreator.Canvas($"http://base/0/canvases/first")
                .WithImage()
                .Build()
        };

        var canvasPaintings = new List<CanvasPainting>()
        {
            new()
            {
                CanvasOriginalId = new Uri("http://base/0/canvases/first"), CanvasOrder = 0, ChoiceOrder = 0,
                Id = "first"
            }
        };
        
        // Act
        var items = canvasPaintings.GenerateProvisionalCanvases(pathGenerator, canvasList, pathRewriteParser);

        // Assert
        var item = items.Single();
        item.Id.Should().Be("http://base/0/canvases/first");
        item.Width.Should().Be(110);
        item.Items[0].Items[0].As<PaintingAnnotation>().Body.As<Image>().Height.Should()
            .Be(100);
    }
    
    [Fact]
    public void GenerateProvisionalCanvases_MixesManifestsAndItems_IfBothSet()
    {
        var canvases = new List<Canvas>
        {
            ManifestTestCreator.Canvas($"http://base/0/canvases/first")
                .WithImage()
                .Build()
        };

        var canvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("http://base/0/canvases/first"), CanvasOrder = 0, ChoiceOrder = 0,
                Id = "first"
            },
            new() { AssetId = new AssetId(1, 1, "someAsset"), CanvasOrder = 1, ChoiceOrder = 0, Id = "second" },
        };
        
        // Act
        var result = canvasPaintings.GenerateProvisionalCanvases(pathGenerator, canvases, pathRewriteParser);
        
        // Assert
        result.Should().HaveCount(2);
        result.First().Should().BeEquivalentTo(canvases.First());
        result.Last().Id.Should().Be("http://base/0/canvases/second");
    }
    
    [Fact]
    public void GenerateProvisionalCanvases_GeneratesProvisionalItem_FromMatchedPaintedResource()
    {
        var canvases = new List<Canvas>
        {
            new()
            {
                Id = "http://base/0/canvases/first",
                Homepage = []
            }
        };

        var canvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("http://base/0/canvases/first"), CanvasOrder = 0, ChoiceOrder = 0,
                Id = "fromProvisional"
            }
        };
        
        // Act
        var result = canvasPaintings.GenerateProvisionalCanvases(pathGenerator, canvases, pathRewriteParser);
        
        // Assert
        result.Should().HaveCount(1);
        result.First().Items.Should().BeNull();
        result.First().Homepage.Should().NotBeNull();
    }
    
    [Fact]
    public void GenerateProvisionalCanvases_PassesBackCompletedItem_FromMatchedPaintedResource()
    {
        var canvases = new List<Canvas>
        {
            new()
            {
                Id = "http://base/0/canvases/first",
                Homepage = [],
                Items =
                [
                    new AnnotationPage
                    {
                        Items =
                        [
                            new PaintingAnnotation
                            {
                                Id = "http://base/0/canvases/first/annotations/0",
                            }
                        ]
                    }
                ]

            }
        };

        var canvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("http://base/0/canvases/first"), CanvasOrder = 0, ChoiceOrder = 0,
                Id = "fromProvisional"
            }
        };
        
        // Act
        var result = canvasPaintings.GenerateProvisionalCanvases(pathGenerator, canvases, pathRewriteParser);
        
        // Assert
        result.Should().HaveCount(1);
        result.First().Items.First().Items.First().As<PaintingAnnotation>().Id.Should()
            .Be("http://base/0/canvases/first/annotations/0");
        result.First().Homepage.Should().NotBeNull();
    }
    
    [Fact]
    public void GenerateProvisionalCanvases_MixesManifestsAndItemsWithChoiceInPaintedResources_IfBothSet()
    {
        var canvases = new List<Canvas>
        {
            ManifestTestCreator.Canvas($"http://base/0/canvases/first")
                .WithImage()
                .Build()
        };

        var canvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("http://base/0/canvases/first"), CanvasOrder = 0, ChoiceOrder = 0,
                Id = "first"
            },
            new() { AssetId = new AssetId(1, 1, "someAsset"), CanvasOrder = 1, ChoiceOrder = 0, Id = "second" },
            new() { AssetId = new AssetId(1, 1, "someAsset"), CanvasOrder = 1, ChoiceOrder = 1, Id = "third" },
        };
        
        // Act
        var result = canvasPaintings.GenerateProvisionalCanvases(pathGenerator, canvases, pathRewriteParser);
        
        // Assert
        result.Should().HaveCount(3);
        result.First().Should().BeEquivalentTo(canvases.First());
        result[1].Id.Should().Be("http://base/0/canvases/second");
        result.Last().Id.Should().Be("http://base/0/canvases/third");
    }
    
    [Fact]
    public void GenerateProvisionalCanvases_MixesManifestsAndItemsWithChoiceInItems_IfBothSet()
    {
        var canvases = new List<Canvas>
        {
            ManifestTestCreator.Canvas($"http://base/0/canvases/first")
                .WithImages(2)
                .Build()
        };

        var canvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                CanvasOriginalId = new Uri("http://base/0/canvases/first"), CanvasOrder = 0, ChoiceOrder = 0,
                Id = "first"
            },
            new()
            {
                CanvasOriginalId = new Uri("http://base/0/canvases/first"), CanvasOrder = 0, ChoiceOrder = 1,
                Id = "first"
            },
            new() { AssetId = new AssetId(1, 1, "someAsset"), CanvasOrder = 1, ChoiceOrder = 0, Id = "third" },
        };
        
        // Act
        var result = canvasPaintings.GenerateProvisionalCanvases(pathGenerator, canvases, pathRewriteParser);
        
        // Assert
        result.Should().HaveCount(2);
        result.First().Should().BeEquivalentTo(canvases.First());
        result.Last().Id.Should().Be("http://base/0/canvases/third");
    }
    
    [Fact]
    public void GenerateProvisionalCanvases_SetsCanvasPaintings_InCanvasThenChoiceOrder()
    {
        // Arrange
        var iiifManifest = new PresentationManifest();
        var customer = 123;

        var canvasPaintings = new List<CanvasPainting>()
        {
            new()
            {
                ChoiceOrder = 1,
                CanvasOrder = 2,
                AssetId = new AssetId(1, 2, "assetId1"),
                CustomerId = customer,
                Id = "assetId1"
            },
            new()
            {
                ChoiceOrder = 2,
                CanvasOrder = 2,
                AssetId = new AssetId(1, 2, "assetId2"),
                CustomerId = customer,
                Id = "assetId1"
            },
            new()
            {
                CanvasOrder = 1,
                AssetId = new AssetId(1, 2, "assetId3"),
                CustomerId = customer,
                Id = "assetId3"
            }
        };
        
        // Act
        var result = canvasPaintings.GenerateProvisionalCanvases(pathGenerator, [], pathRewriteParser);

        // Assert
        result.First().Items.First().Items.First().As<PaintingAnnotation>().Body.Should().BeNull();
        result.Last().Items.First().Items.First().As<PaintingAnnotation>().Body.Should()
            .BeOfType<PaintingChoice>();
    }
    
    [Theory]
    [InlineData("https://foo.com/0/canvases/foo")]
    [InlineData("foo")]
    public void GenerateProvisionalCanvases_RewritesProvisionalItemId_FromMatchedPaintedResource(string canvasId)
    {
        var canvases = new List<Canvas>
        {
            new()
            {
                Id = canvasId,
                Homepage = 
                [
                    new Image
                    {
                        Id = "https://foo.com/0/homepage/foo",
                    }
                ]
            }
        };

        var canvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                CanvasOrder = 0, ChoiceOrder = 0, Id = "foo"
            }
        };
        
        // Act
        var result = canvasPaintings.GenerateProvisionalCanvases(pathGenerator, canvases, pathRewriteParser);
        
        // Assert
        result.Should().HaveCount(1);
        var firstCanvas = result.First();
        firstCanvas.Items.First().Items.First().As<PaintingAnnotation>().Id.Should()
            .Be("http://base/0/canvases/foo/annotations/0");
        firstCanvas.Homepage.First().Id.Should().Be("https://foo.com/0/homepage/foo");
        result.First().Homepage.Should().NotBeNull();
    }
}
