﻿using API.Converters;
using API.Helpers;
using API.Tests.Helpers;
using Models.API.Manifest;
using Models.Database.General;
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
        var result = iiifManifest.SetGeneratedFields(dbManifest, pathGenerator, manifest => manifest.Hierarchy.Last());

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
                    CanvasOrder = 100
                }
            ]
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, pathGenerator);

        // Assert
        var cp = result.PaintedResources.Single().CanvasPainting;
        cp.CanvasId.Should().Be("http://base/123/canvases/the-canvas");
        cp.ChoiceOrder.Should().Be(10);
        cp.CanvasOrder.Should().Be(100);
        cp.CanvasOriginalId.Should().Be("http://example.test/canvas1");
    }
}