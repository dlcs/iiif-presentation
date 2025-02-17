using API.Converters;
using API.Tests.Helpers;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Models.API.Manifest;
using Models.Database.General;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository.Paths;
using CanvasPainting = Models.Database.CanvasPainting;
using DBManifest = Models.Database.Collections.Manifest;

namespace API.Tests.Converters;

public class ManifestConverterTests
{
    private readonly IPathGenerator pathGenerator = TestPathGenerator.CreatePathGenerator("base", Uri.UriSchemeHttp);
    
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
    public void SetGeneratedFields_SetsCanvasPaintings_InCanvasThenChoiceOrder()
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
                    ChoiceOrder = 1,
                    CanvasOrder = 2,
                    AssetId = new AssetId(1, 2, "assetId1")
                },
                new CanvasPainting
                {
                    ChoiceOrder = 2,
                    CanvasOrder = 2,
                    AssetId = new AssetId(1, 2, "assetId2")
                },
                new CanvasPainting
                {
                    CanvasOrder = 1,
                    AssetId = new AssetId(1, 2, "assetId3")
                }
            ]
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, pathGenerator);

        // Assert
        result.PaintedResources.Should()
            .BeInAscendingOrder(pr => pr.CanvasPainting.CanvasOrder)
            .And.ThenBeInAscendingOrder(pr => pr.CanvasPainting.ChoiceOrder);
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
    public void SetGeneratedFields_SetsItems_IfNotSet()
    {
        var iiifManifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    Asset = new JObject
                    {
                        ["id"] = "1b",
                        ["mediaType"] = "image/jpeg"
                    },
                    CanvasPainting = new Models.API.Manifest.CanvasPainting
                    {
                        CanvasOrder = 1, ChoiceOrder = 2, CanvasId = "foo"
                    }
                },
                new PaintedResource
                {
                    Asset = new JObject
                    {
                        ["id"] = "1a",
                        ["mediaType"] = "image/jpeg"
                    },
                    CanvasPainting = new Models.API.Manifest.CanvasPainting
                    {
                        CanvasOrder = 1, ChoiceOrder = 1, CanvasId = "foo"
                    }
                },
                new PaintedResource
                {
                    Asset = new JObject
                    {
                        ["id"] = "2",
                        ["mediaType"] = "image/jpeg"
                    },
                    CanvasPainting = new Models.API.Manifest.CanvasPainting
                    {
                        CanvasOrder = 2, CanvasId = "foo"
                    }
                },
            ]
        };

        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Id = "test-manifest",
            CanvasPaintings =
            [
                new CanvasPainting
                    { AssetId = new AssetId(1, 2, "1b"), CanvasOrder = 1, ChoiceOrder = 2, Id = "first" },
                new CanvasPainting
                    { AssetId = new AssetId(1, 2, "1a"), CanvasOrder = 1, ChoiceOrder = 2, Id = "first" },
                new CanvasPainting { AssetId = new AssetId(1, 2, "2"), CanvasOrder = 2, Id = "second" },
            ],
            Hierarchy = [new Hierarchy { Slug = "slug", ManifestId = "test-manifest", Canonical = true }]
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
                        Id = "http://base/0/canvases/first/annopages/1",
                        Items =
                        [
                            new PaintingAnnotation
                            {
                                Id = "http://base/0/canvases/first/annotations/1",
                                Behavior = [Models.Infrastructure.Behavior.Processing],
                                Target = new Canvas { Id = "http://base/0/canvases/first" },
                                Body = new PaintingChoice
                                {
                                    Items =
                                    [
                                        new Image(), new Image()
                                    ]
                                }
                            }
                        ]
                    }
                ]
            },
            new()
            {
                Id = "http://base/0/canvases/second",
                Items =
                [
                    new AnnotationPage
                    {
                        Id = "http://base/0/canvases/second/annopages/2",
                        Items =
                        [
                            new PaintingAnnotation
                            {
                                Id = "http://base/0/canvases/second/annotations/2",
                                Behavior = [Models.Infrastructure.Behavior.Processing],
                                Target = new Canvas { Id = "http://base/0/canvases/second" },
                                Body = new Image()
                            }
                        ]
                    }
                ]
            }
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, pathGenerator);
        
        // Assert
        result.Items.Should().BeEquivalentTo(expectedItems);
    }
    
    [Fact]
    public void SetGeneratedFields_DoesNotUpdateItems_IfSet()
    {
        var iiifManifest = new PresentationManifest
        {
            Items = new List<Canvas>
            {
                new()
                {
                    Id = "http://base/0/canvases/first",
                    Items =
                    [
                        new AnnotationPage
                        {
                            Id = "http://base/0/canvases/first/annopages/1",
                            Items =
                            [
                                new PaintingAnnotation
                                {
                                    Id = "http://base/0/canvases/first/annotations/1",
                                    Behavior = [Models.Infrastructure.Behavior.Processing],
                                    Target = new Canvas { Id = "http://base/0/canvases/first" },
                                    Body = new PaintingChoice
                                    {
                                        Items =
                                        [
                                            new Image(), new Image()
                                        ]
                                    }
                                }
                            ]
                        }
                    ]
                },
            }
        };

        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Id = "test-manifest",
            CanvasPaintings =
            [
                new CanvasPainting
                    { CanvasOriginalId = new Uri("http://base/0/canvases/first"), CanvasOrder = 1, ChoiceOrder = 2, Id = "first" },
                new CanvasPainting
                    { CanvasOriginalId = new Uri("http://base/0/canvases/first"), CanvasOrder = 1, ChoiceOrder = 2, Id = "first" },
            ],
            Hierarchy = [new Hierarchy { Slug = "slug", ManifestId = "test-manifest", Canonical = true }]
        };

        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, pathGenerator);
        
        // Assert
        result.Items.Should().BeEquivalentTo(iiifManifest.Items, "Items untouched as already present");
    }
}
